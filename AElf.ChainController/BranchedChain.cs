// ReSharper disable once CheckNamespace

using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public class BranchedChain
    {
        public BranchedChain(PendingBlock first, IEnumerable<PendingBlock> list)
        {
            PendingBlocks.Add(first);
            
            foreach (var pendingBlock in list)
            {
                PendingBlocks.Add(pendingBlock);
            }
        }
        
        public BranchedChain(IEnumerable<PendingBlock> list, PendingBlock last)
        {
            foreach (var pendingBlock in list)
            {
                PendingBlocks.Add(pendingBlock);
            }

            PendingBlocks.Add(last);
        }
        
        public BranchedChain(IEnumerable<PendingBlock> list1, IEnumerable<PendingBlock> list2)
        {
            foreach (var pendingBlock in list1)
            {
                PendingBlocks.Add(pendingBlock);
            }
            
            foreach (var pendingBlock in list2)
            {
                PendingBlocks.Add(pendingBlock);
            }
        }

        public BranchedChain(PendingBlock first)
        {
            PendingBlocks.Add(first);
        }

        public bool CanCheckout(ulong localHeight)
        {
            return IsContinuous && EndHeight > localHeight;
        }

        public bool IsContinuous
        {
            get
            {
                if (PendingBlocks.Count <= 0)
                {
                    return false;
                }

                var preBlockHash = PendingBlocks[0].BlockHash;
                for (var i = 1; i < PendingBlocks.Count; i++)
                {
                    if (PendingBlocks[i].BlockHash != preBlockHash)
                    {
                        return false;
                    }

                    preBlockHash = PendingBlocks[i].BlockHash;
                }

                return true;
            }
        }
        public ulong EndHeight { get; set; }
        public List<PendingBlock> PendingBlocks { get; set; }

        public Hash PreBlockHash => PendingBlocks.Count <= 0 ? null : PendingBlocks.First().Block.Header.PreviousBlockHash;
        public Hash LastBlockHash => PendingBlocks.Count <= 0 ? null : PendingBlocks.Last().BlockHash;
    }
}