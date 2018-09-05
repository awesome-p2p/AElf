using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public interface IBlockCollection
    {
        List<PendingBlock> PendingBlocks { get; set; }
        List<PendingBlock> PendingForkBlocks { get; set; }
        void AddPendingBlock(PendingBlock pendingBlock);
        void RemovePendingBlock(PendingBlock pendingBlock);
    }
}