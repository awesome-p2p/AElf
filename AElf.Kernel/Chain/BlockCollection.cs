using System;
using System.Collections.Generic;
using System.Linq;
using AElf.Cryptography.ECDSA;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public class BlockCollection : IBlockCollection
    {
        private readonly Dictionary<DataPath, IBlock> _blocks = new Dictionary<DataPath, IBlock>();
        private readonly BranchedChain _branchedChain;

        public List<PendingBlock> PendingBlocks { get; set; } = new List<PendingBlock>();
        /// <summary>
        /// Should with same height
        /// </summary>
        public List<PendingBlock> PendingForkBlocks { get; set; }

        public BlockCollection()
        {
            _branchedChain = new BranchedChain();
        }

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
        }

        public void RemovePendingBlock(PendingBlock pendingBlock)
        {
            PendingBlocks.Remove(pendingBlock);
        }

        public void AddBlock(IBlock block)
        {
            var uncompressedPrivKey = block.Header.P.ToByteArray();
            var recipientKeyPair = ECKeyPair.FromPublicKey(uncompressedPrivKey);
            
            _blocks.Add(new DataPath
            {
                ChainId = block.Header.ChainId,
                BlockHeight = block.Header.Index,
                BlockProducerAddress = recipientKeyPair.GetAddress()
            }, block);
        }

        public IBlock GetBlockOfBlockProducer(Hash address)
        {
            if (address.ToHex().Length != ECKeyPair.AddressLength)
            {
                throw new InvalidOperationException("Invalid block producer account address.");
            }

            return (from pair in _blocks
                where pair.Key.BlockProducerAddress == address
                select pair.Value).First();
        }

        public IBlock GetBlockOfHeight(ulong height)
        {
            return (from pair in _blocks
                where pair.Key.BlockHeight == height
                select pair.Value).First();
        }

        public IEnumerable<IBlock> GetBlockOfChain(Hash chainId)
        {
            return from pair in _blocks
                where pair.Key.ChainId == chainId
                select pair.Value;
        }
    }
}