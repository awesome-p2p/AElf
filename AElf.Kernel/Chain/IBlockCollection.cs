using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public interface IBlockCollection
    {
        void AddBlock(IBlock block);
        IBlock GetBlockOfBlockProducer(Hash address);
        IBlock GetBlockOfHeight(ulong height);
        IEnumerable<IBlock> GetBlockOfChain(Hash chainId);
    }
}