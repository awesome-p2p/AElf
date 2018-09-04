using System;
using System.Collections.Generic;
using System.Linq;
using AElf.Cryptography.ECDSA;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public class BlockCollection
    {
        private readonly Dictionary<DataPath, IBlock> _blocks = new Dictionary<DataPath, IBlock>();

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