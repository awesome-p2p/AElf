using System.Collections.Generic;
using System.Linq;

namespace AElf.Kernel
{
    public static class PendingBlockExtensions
    {
        public static void ClearListIfHeightLessThan(this List<PendingBlock> pendingBlocks, ulong height)
        {
            if (pendingBlocks.Count <= 0)
            {
                return;
            }

            var currentHeight = pendingBlocks.First().Block.Header.Index;
            if (currentHeight < height)
            {
                pendingBlocks.Clear();
            }
        }
        
        public static void AddPendingBlock(this List<PendingBlock> pendingBlocks, PendingBlock pendingBlock)
        {
            if (pendingBlocks.Count <= 0)
            {
                pendingBlocks.Add(pendingBlock);
            }
            else
            {
                var currentHeight = pendingBlocks.First().Block.Header.Index;
                var newHeight = pendingBlock.Block.Header.Index;
                if (currentHeight < newHeight)
                {
                    pendingBlocks.Clear();
                    pendingBlocks.Add(pendingBlock);
                }
                else if (currentHeight == newHeight)
                {
                    pendingBlocks.Add(pendingBlock);
                }
            }
        }
    }
}