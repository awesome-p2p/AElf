﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Kernel.Managers;
using AElf.Kernel.Storages;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal.Networking;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public class BlockChain : LightChain, IBlockChain
    {
        private readonly ITransactionManager _transactionManager;

        public BlockChain(Hash chainId, IChainManagerBasic chainManager, IBlockManagerBasic blockManager,
            ITransactionManager transactionManager, IDataStore dataStore) : base(
            chainId, chainManager, blockManager, dataStore)
        {
            _transactionManager = transactionManager;
        }

        public IBlock CurrentBlock
        {
            get
            {
                var currentBlockHash = _chainManager.GetCurrentBlockHashAsync(_chainId).Result;
                return _blockManager.GetBlockAsync(currentBlockHash).Result;
            }
        }

        public async Task<bool> HasBlock(Hash blockId)
        {
            var blk = await _blockManager.GetBlockAsync(blockId);
            return blk != null;
        }

        public async Task<bool> IsOnCanonical(Hash blockId)
        {
            throw new NotImplementedException();
        }

        private async Task AddBlockAsync(IBlock block)
        {
            await AddHeaderAsync(block.Header);
            await _blockManager.AddBlockBodyAsync(block.Header.GetHash(), block.Body);
        }

        public async Task AddBlocksAsync(IEnumerable<IBlock> blocks)
        {
            foreach (var block in blocks)
            {
                await AddBlockAsync(block);
            }
        }

        public async Task<IBlock> GetBlockByHashAsync(Hash blockHash)
        {
            return await _blockManager.GetBlockAsync(blockHash);
        }

        public async Task<IBlock> GetBlockByHeightAsync(ulong height)
        {
            var header = await GetHeaderByHeightAsync(height);
            if (header == null)
            {
                return null;
            }

            return await GetBlockByHashAsync(header.GetHash());
        }

        public async Task<List<Transaction>> RollbackToHeight(ulong height)
        {   
            var currentHash = await GetCurrentBlockHashAsync();
            var currentHeight = ((BlockHeader) await GetHeaderByHashAsync(currentHash)).Index;
            
            var txs = new List<Transaction>();
            if (currentHeight == height)
            {
                return txs;
            }

            //Just for logging
            for (var i = currentHeight - 1; i > height; i--)
            {
                var block = await GetBlockByHeightAsync(i);
                var body = block.Body;
                foreach (var txId in body.Transactions)
                {
                    var tx = await _transactionManager.GetTransaction(txId);
                    txs.Add(tx);
                }
            }

            for (var i = currentHeight - 1; i > height; i--)
            {
                await _dataStore.RemoveAsync<Hash>(GetHeightHash(currentHeight).OfType(HashType.CanonicalHash));
            }

            var hash = await GetCanonicalHashAsync(height);
            
            await _chainManager.UpdateCurrentBlockHashAsync(_chainId, hash);
            
            return txs;
        }
    }
}