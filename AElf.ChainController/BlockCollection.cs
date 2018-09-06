using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public class BlockCollection : IBlockCollection
    {
        private readonly List<BranchedChain> _branchedChains= new List<BranchedChain>();
        private ulong _localLatestBlockHeight;

        public List<PendingBlock> PendingBlocks { get; set; } = new List<PendingBlock>();
        /// <summary>
        /// Should with same height
        /// </summary>
        public List<PendingBlock> PendingForkBlocks { get; set; }

        public void AddPendingBlock(PendingBlock pendingBlock)
        {
            if (PendingBlocks.Any(b => b.BlockHash == pendingBlock.BlockHash) ||
                PendingForkBlocks.Any(b => b.BlockHash == pendingBlock.BlockHash))
            {
                // No need to handle this pending block again.
                return;
            }

            if (PendingBlocks.Any(b => b.Block.Header.Index == pendingBlock.Block.Header.Index))
            {
                if (PendingBlocks.Any(b => b.Block.Header.Index > pendingBlock.Block.Header.Index))
                {
                    // If the pending blocks list exists a block with same and higher height, just ignore this block.
                    return;
                }
                PendingForkBlocks.AddPendingBlock(pendingBlock);
                return;
            }
            
            PendingBlocks.AddPendingBlock(pendingBlock);
            _localLatestBlockHeight = Math.Max(_localLatestBlockHeight, pendingBlock.Block.Header.Index);
        }

        public void RemovePendingBlock(PendingBlock pendingBlock)
        {
            PendingBlocks.Remove(pendingBlock);
            AddBlockToBranchedChains(pendingBlock);
        }

        public void AddBlockToBranchedChains(PendingBlock pendingBlock)
        {
            var preBlockHash = pendingBlock.Block.Header.PreviousBlockHash;
            var blockHash = pendingBlock.BlockHash;
            var toRemove = new List<BranchedChain>();
            var toAdd = new List<BranchedChain>();
            foreach (var branchedChain in _branchedChains)
            {
                if (branchedChain.PendingBlocks.First().Block.Header.PreviousBlockHash == blockHash)
                {
                    var newBranchedChain = new BranchedChain(pendingBlock, branchedChain.PendingBlocks);
                    toAdd.Add(newBranchedChain);
                    toRemove.Add(branchedChain);
                }
                else if (branchedChain.PendingBlocks.Last().BlockHash == preBlockHash)
                {
                    var newBranchedChain = new BranchedChain(branchedChain.PendingBlocks, pendingBlock);
                    toAdd.Add(newBranchedChain);
                    toRemove.Add(branchedChain);
                }
                else
                {
                    toAdd.Add(new BranchedChain(pendingBlock));
                }
            }

            foreach (var branchedChain in toRemove)
            {
                _branchedChains.Remove(branchedChain);
            }

            foreach (var branchedChain in toAdd)
            {
                _branchedChains.Add(branchedChain);
            }

            var result = AdjustBranchedChains();
            if (result != null)
            {
                PendingBlocks = result.PendingBlocks;
            }
        }

        public BranchedChain AdjustBranchedChains()
        {
            var preBlockHashes = new List<Hash>();
            var lastBlockHashes = new List<Hash>();

            foreach (var branchedChain in _branchedChains)
            {
                preBlockHashes.Add(branchedChain.PreBlockHash);
                lastBlockHashes.Add(branchedChain.LastBlockHash);
            }

            var pair = new List<Hash>();

            foreach (var preBlockHash in preBlockHashes)
            {
                foreach (var lastBlockHash in lastBlockHashes)
                {
                    if (preBlockHash == lastBlockHash)
                    {
                        pair.Add(preBlockHash);
                    }
                }
            }

            foreach (var hash in pair)
            {
                var chain1 = _branchedChains.First(c => c.PreBlockHash == hash);
                var chain2 = _branchedChains.First(c => c.LastBlockHash == hash);
                _branchedChains.Remove(chain1);
                _branchedChains.Remove(chain2);
                _branchedChains.Add(new BranchedChain(chain1.PendingBlocks, chain2.PendingBlocks));
            }

            foreach (var branchedChain in _branchedChains)
            {
                if (branchedChain.CanCheckout(_localLatestBlockHeight))
                {
                    return branchedChain;
                }
            }

            return null;
        }
    }
}