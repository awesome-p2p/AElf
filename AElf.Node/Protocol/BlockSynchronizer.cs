﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.ChainController.TxMemPool;
using AElf.Common.ByteArrayHelpers;
using AElf.Common.Extensions;
using AElf.Kernel;
using AElf.Kernel.Node.Protocol.Exceptions;
using AElf.Network;
using AElf.Network.Peers;
using AElf.Node.AElfChain;
using AElf.Node.Protocol.Events;
using NLog;

[assembly: InternalsVisibleTo("AElf.Kernel.Tests")]

namespace AElf.Node.Protocol
{
    /// <summary>
    /// When a node starts it creates this BlockSynchroniser for two reasons: the first
    /// is that the node is very probably behind other nodes on the network and it needs
    /// to perform an initial download of the already processed blocks; the second reason
    /// this is needed is that when the node receives a block, it's possible that some of 
    /// the transactions are not in the pool. In the later case, the block is placed here 
    /// and requests are sent out to retrieve missing transactions. These two operation are
    /// possibly performed at the same time, even though the Initial sync will at one point
    /// stop because we'll be receiving the new blocks through the network.
    ///
    /// For state consistency, we use _chainContextService.PendingBlocks to store all the
    /// blocks with their indexes higher than local latest block height.
    /// </summary>
    public class BlockSynchronizer : IBlockSynchronizer
    {
        public event EventHandler SyncFinished;

        private readonly INetworkManager _networkManager;
        private readonly ILogger _logger;
        private readonly IChainContextService _chainContextService;

        public List<PendingBlock> PendingBlocks => _chainContextService.PendingBlocks;
        
        public bool ShouldDoInitialSync { get; private set; } = false;
        public bool IsInitialSyncInProgress { get; private set; } = false;

        public int CurrentExecHeight = 1;

        public int SyncTargetHeight;
        private int MaxOngoingBlockRequests = 10;
        private readonly List<int> _currentBlockRequests;

        public int MaxOngoingTxRequests = 10;
        
        private readonly object _currentBlockRequestLock = new object();

        private MainchainNodeService _mainChainNode;
        private readonly ITxPoolService _poolService;

        private readonly BlockingCollection<Job> _jobQueue;

        public BlockSynchronizer(INetworkManager networkManager, ITxPoolService poolService, IChainContextService chainContextService)
        {
            _jobQueue = new BlockingCollection<Job>();
            _currentBlockRequests = new List<int>();

            _poolService = poolService;
            _chainContextService = chainContextService;
            _networkManager = networkManager;

            _logger = LogManager.GetLogger("BlockSync");

            _networkManager.BlockReceived += OnBlockReceived;
            _networkManager.TransactionsReceived += OnTransactionReceived;
        }

        private void OnTransactionReceived(object sender, EventArgs e)
        {
            if (e is TransactionsReceivedEventArgs txsEventArgs)
            {
                HandleTransactionMessage(txsEventArgs);
            }
        }

        private void OnBlockReceived(object sender, EventArgs e)
        {
            if (e is BlockReceivedEventArgs blockReceivedArgs)
            {
                try
                {
                    EnqueueJob(new Job {Block = blockReceivedArgs.Block, Peer = blockReceivedArgs.Peer});
                }
                catch (Exception ex)
                {
                    _logger?.Error(ex, "Error while enqueing HandleBlockReception.");
                }
            }
        }

        /// <summary>
        /// Starts the sync process with, optionally an initial sync mode. The
        /// synchronizer considers the initial sync finished when all the block
        /// below the sync target have been successfully executed by the node.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="doInitialSync"></param>
        /// <returns></returns>
        // todo remove The node property : autofac circular dependency problem.
        public async Task Start(MainchainNodeService node, bool doInitialSync)
        {
            _mainChainNode = node;

            CurrentExecHeight = node.GetCurrentHeight() + 1;
            _logger?.Debug($"Initial chain height {CurrentExecHeight}.");

            ShouldDoInitialSync = doInitialSync;
            IsInitialSyncInProgress = false; // started by first block

            if (doInitialSync)
                _logger?.Trace("Initial sync started.");
            else
                Task.Run(() => DoSync());
        }

        /// <summary>
        /// Handles a list of transactions sent by another node, these transactions are either
        /// issues from a broadcast or a request.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        private async Task HandleTransactionMessage(TransactionsReceivedEventArgs txsEventArgs)
        {
            var txs = txsEventArgs.Transactions;
            
            _logger?.Debug($"Handling transaction list message : {txs.ToDebugString()} from {txsEventArgs.Peer}");

            var receivedTxs = new List<byte[]>();
            
            foreach (var tx in txs.Transactions)
            {
                try
                {
                    // Every received transaction, provided that it's valid, should be in the pool.
                    var result = await _poolService.AddTxAsync(tx);
                    
                    if (result == TxValidation.TxInsertionAndBroadcastingError.Success)
                    {
                        receivedTxs.Add(tx.GetHashBytes());
                    }
                    else
                    {
                        _logger?.Warn($"Failed to add the following transaction to the pool (reason: {result}): "
                                       + $"{tx.GetTransactionInfo()}");
                    }
                }
                catch (Exception e)
                {
                    _logger?.Error(e, "Error while hadling transactions.");
                }
            }
            
            EnqueueJob(new Job { Transactions = receivedTxs });
        }

        public void IncrementChainHeight()
        {
            Interlocked.Increment(ref CurrentExecHeight);
            _logger?.Debug("Height has been incremented, new value: " + CurrentExecHeight);
        }

        public void EnqueueJob(Job job)
        {
            try
            {
                if (job?.Block?.Header != null && ShouldDoInitialSync && !IsInitialSyncInProgress)
                {
                    // The first block we receive when a node should synchronize will determine the 
                    // target height
                    SyncTargetHeight = (int) job.Block.Header.Index;
                    IsInitialSyncInProgress = true;

                    // Start the sync
                    Task.Run(() => DoSync());

                    _logger?.Debug($"Initial synchronisation has started at target height {SyncTargetHeight}.");
                }

                // Enqueue the job
                Task.Run(() => { _jobQueue.Add(job); }).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger?.Error("Error while adding " + job?.Block?.GetHash().Value.ToByteArray().ToHex());
            }
        }

        internal void RequestNextBlocks()
        {
            _logger?.Debug($"Request state, current height {CurrentExecHeight}, sync target {SyncTargetHeight}, current requests {_currentBlockRequests.Count}");
            
            lock (_currentBlockRequestLock)
            {
                for (int i = CurrentExecHeight; i < SyncTargetHeight && _currentBlockRequests.Count <= MaxOngoingBlockRequests; i++)
                {
                    if (!_currentBlockRequests.Contains(i))
                    {
                        _currentBlockRequests.Add(i);
                        _networkManager.QueueBlockRequestByIndex(i);
                        
                        Thread.Sleep(5);
                
                        _logger?.Debug($"Requested block at index {i}.");
                    }
                }
            }
        }

        private void DoSync()
        {
            // Main work loop.
            while (true)
            {
                Job job = null;

                try
                {
                    job = _jobQueue.Take();
                }
                catch (Exception e)
                {
                    _logger?.Error("Error while dequeuing " + job?.Block.GetHash().ToHex());
                    continue;
                }
                
                // The next block of code will process the Job (Transactions to process or a block)
                // 1. Take job
                // 2. Exec
                // 3. Request 

                try
                {
                    if (job.Transactions != null)
                    {
                        // Some transactions were queued for processing 
                        SetTransactions(job.Transactions);
                    }
                    else
                    {
                        // A block was queued for processing 
                        var addtionResult = AddBlockToSync(job.Block, job.Peer).Result;

                        if (!addtionResult)
                            _logger?.Warn("Could not add block to sync");
                    }

                    if (PendingBlocks.Count <= 0)
                    {
                        _logger.Trace("No pending blocks");
                        continue;
                    }

                    // Log sync info
                    var syncedCount = PendingBlocks.Count(pb => pb.IsSynced);
                    _logger?.Trace(
                        $"There's {PendingBlocks.Count} pending blocks, " +
                        $"with synced : {syncedCount}, non-synced : {PendingBlocks.Count - syncedCount}");

                    // Get the blocks that are fully synched
                    var pendingBlocks = GetBlocksToExecute();

                    if (pendingBlocks != null && pendingBlocks.Any())
                    {
                        var str2 = pendingBlocks.Select(bb => bb.ToString()).Aggregate((i, jf) => i + " || " + jf);
                        _logger?.Trace("Chosen for execution: " + str2);

                        if (string.IsNullOrEmpty(str2))
                            _logger?.Trace("Nobody chosen for execution.");

                        if (pendingBlocks != null && pendingBlocks.Count > 0)
                        {
                            // exec
                            var executedBlocks = TryExecuteBlocks(pendingBlocks).Result;
                            _logger?.Trace("Executed the blocks with the following index(es) : " +
                                           GetPendingBlockListLog(executedBlocks));
                        }
                    }

                    // After execution request the following batch of transactions
                    RequestMissingTxs();
                    
                    if (IsInitialSyncInProgress)
                        RequestNextBlocks();
                }
                catch (Exception e)
                {
                    _logger?.Error(e, "Error while dequeuing and processing job.");
                }
            }
        }

        private string GetPendingBlockListLog(List<PendingBlock> blocks)
        {
            if (blocks == null || blocks.Count <= 0)
                return "[ ]";
            
            var brString = new StringBuilder();
            brString.Append(blocks.ElementAt(0).Block.Header.Index);

            for (var i = 1; i < blocks.Count; i++)
            {
                brString.Append(" - " + blocks.ElementAt(i).Block.Header.Index);
            }

            return brString.ToString();
        }

        private void RequestMissingTxs()
        {
            var listOfMissingTxToRequest = new List<KeyValuePair<byte[], IPeer>>();

            foreach (var pdBlock in PendingBlocks)
            {
                if (!pdBlock.IsSynced)
                {
                    foreach (var tx in pdBlock.MissingTxs.Where(m => !m.IsRequestInProgress))
                    {
                        if (listOfMissingTxToRequest.Count >= MaxOngoingTxRequests)
                            break;

                        listOfMissingTxToRequest.Add(new KeyValuePair<byte[], IPeer>(tx.Hash, pdBlock.Peer));
                        tx.IsRequestInProgress = true;
                    }
                }
            }

            if (listOfMissingTxToRequest.Any())
            {
                _networkManager.QueueTransactionRequest(listOfMissingTxToRequest.Select(kvp => kvp.Key).ToList(), listOfMissingTxToRequest.First().Value);
                _logger?.Debug($"Requested the following {listOfMissingTxToRequest.Count} transactions [" + string.Join(", ", listOfMissingTxToRequest.Select(kvp => kvp.Key.ToHex())) + "]");
            }
        }

        internal List<PendingBlock> GetBlocksToExecute()
        {
            // Calculate the next batch to execute
            var ordered = PendingBlocks.Where(p => p.IsSynced).OrderBy(p => p.Block.Header.Index).ToList();

            if (ordered.Count <= 0)
                return new List<PendingBlock>();

            var pending = new List<PendingBlock>();
            var currentIndex = (int) ordered[0].Block.Header.Index;

            if (ShouldDoInitialSync && currentIndex > CurrentExecHeight)
                return null;

            for (var i = 0; i < ordered.Count; i++)
            {
                pending.Add(ordered[i]);

                if (i + 1 >= ordered.Count || (int) ordered[i + 1].Block.Header.Index > currentIndex + 1)
                    break;

                currentIndex = (int) ordered[i + 1].Block.Header.Index;
            }

            return pending;
        }

        /// <summary>
        /// When a block is received through the network it is placed here for sync
        /// purposes. In the case that the transaction was not received through the
        /// network, it will be placed here to sync.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="peer"></param>
        private async Task<bool> AddBlockToSync(IBlock block, IPeer peer)
        {
            if (block?.Header == null || block.Body == null)
                throw new InvalidBlockException("The block, blockheader or body is null");

            byte[] blockHash;
            try
            {
                blockHash = block.GetHash().GetHashBytes();
            }
            catch (Exception e)
            {
                throw new InvalidBlockException("Invalid block hash");
            }

            if (GetBlock(blockHash) != null)
            {
                _logger?.Warn("Block already in pending list.");
                return false;
            }

            var missingTxs = _poolService.GetMissingTransactions(block);
            if (missingTxs == null)
            {
                _logger?.Warn("Unknown exception in the pool.");
                return false;
            }

            // todo check that the returned txs are actually in the block
            var newPendingBlock = new PendingBlock(blockHash, block, missingTxs) { Peer = peer };

            _chainContextService.AddPendingBlock(newPendingBlock);

            _logger?.Debug($"Added block to sync {{ id : {blockHash.ToHex()}, index : {block.Header.Index}, tx-count : {block.Body.Transactions.Count} }} ");

            return true;
        }

        private readonly object _objLock = new object();

        /// <summary>
        /// Tries to executes the specified blocks.
        /// </summary>
        /// <param name="pendingBlocks"></param>
        /// <returns></returns>
        internal async Task<List<PendingBlock>> TryExecuteBlocks(List<PendingBlock> pendingBlocks)
        {
            var toRemove = new List<PendingBlock>();
            var executed = new List<PendingBlock>();

            var blocks = pendingBlocks.ToList();
            foreach (var pendingBlock in blocks)
            {
                var block = pendingBlock.Block;

                var res = await _mainChainNode.ExecuteAndAddBlock(block);

                var blockHexHash = block.GetHash().Value.ToByteArray().ToHex();
                int blockIndex = (int)block.Header.Index;
                
                if (res.SyncSuggestion == SyncSuggestion.Apply)
                {
                    if (res.ExecutionResult == ExecutionResult.Success)
                    {
                        // If success: remove current pending block.
                        toRemove.Add(pendingBlock);
                        executed.Add(pendingBlock);
                    
                        Interlocked.Increment(ref CurrentExecHeight);
                    
                        lock (_currentBlockRequestLock)
                        {
                            _currentBlockRequests.Remove(blockIndex);
                        }
                    
                        _logger?.Debug($"Block {{ id : {blockHexHash}, index: {blockIndex} }}  was successfully executed.");
                    }
                    else
                    {
                        // Validation was successfull, but execution failed.
                        _logger?.Warn($"Block {{ id : {blockHexHash}, index: {blockIndex} }}  was not executed.");
                    }
                }
                else
                {
                    // Somehow failed
                    if (res.SyncSuggestion == SyncSuggestion.Abandon)
                    {
                        // The block is an earlier block and one with the same
                        // height as already been executed so it can safely be
                        // remove from the pending blocks.
                        toRemove.Add(pendingBlock);

                        if (IsInitialSyncInProgress && (int) pendingBlock.Block.Header.Index == CurrentExecHeight)
                        {
                            Interlocked.Increment(ref CurrentExecHeight);
                        }
                        
                        _logger?.Warn($"Block {{ id : {blockHexHash}, index: {blockIndex} }} " +
                                      $"ignored because validation returned {res.SyncSuggestion}.");
                    }
                    else if (res.SyncSuggestion == SyncSuggestion.RequestMissingBlocks)
                    {
                        // Current block's index is higher than local current height,
                        // which means we may missing some blocks
                        if (ShouldDoInitialSync || (int) block.Header.Index <= CurrentExecHeight)
                            continue;
                        
                        _networkManager.QueueBlockRequestByIndex(CurrentExecHeight);
                        _logger?.Warn($"Block {{ id : {blockHexHash}, index: {blockIndex} }} is pending, " +
                                      $"requesting block with index {CurrentExecHeight}.");

                        break;
                    }
                    else
                    {
                        _logger?.Warn($"Block execution failed: {res.ExecutionResult}, {res.SyncSuggestion} - {{ id : {blockHexHash}, index: {blockIndex} }}");
                    }
                }
            }

            // remove the pending blocks
            foreach (var pdBlock in toRemove)
            {
                lock (_objLock)
                {
                    _chainContextService.RemovePendingBlock(pdBlock);
                }
            }

            if (ShouldDoInitialSync && CurrentExecHeight >= SyncTargetHeight)
            {
                ShouldDoInitialSync = false;
                IsInitialSyncInProgress = false;
                
                _logger?.Debug("Initial sync is finished at height: " + CurrentExecHeight);

                SyncFinished?.Invoke(this, EventArgs.Empty);
            }

            return executed;
        }

        /// <summary>
        /// This adds a transaction to one off the blocks. Typically this happens when
        /// a transaction has been received throught the network (requested by this
        /// synchronizer).
        /// It removes the transaction from the corresponding missing block.
        /// </summary>
        /// <param name="txHashes"></param>
        private void SetTransactions(List<byte[]> txHashes)
        {
            foreach (var txHash in txHashes)
            {
                var block = RemoveTxFromBlock(txHash);

                if (block == null)
                {
                    _logger?.Warn($"The following transaction was not found in any of the blocks : {txHash.ToHex()}");
                }
                else
                {
                    var blockHexHash = block.Block?.GetHash().Value.ToByteArray().ToHex();
                    int blockIndex = (int)block.Block?.Header?.Index;
                    _logger?.Debug($"Transaction {{ id: {txHash.ToHex()} }} synced from block {{ id : {blockHexHash}, index: {blockIndex} }}");
                }
            }
        }

        public PendingBlock GetBlock(byte[] hash)
        {
            return PendingBlocks?.FirstOrDefault(p => p.BlockHash.BytesEqual(hash));
        }

        public PendingBlock RemoveTxFromBlock(byte[] hash)
        {
            foreach (var pdBlock in PendingBlocks)
            {
                foreach (var msTx in pdBlock.MissingTxs)
                {
                    if (msTx.Hash.BytesEqual(hash))
                    {
                        pdBlock.RemoveTransaction(msTx.Hash);
                        return pdBlock;
                    }
                }
            }

            return null;
        }
    }
}