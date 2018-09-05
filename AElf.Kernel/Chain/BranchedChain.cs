// ReSharper disable once CheckNamespace

using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public class BranchedChain
    {
        public ulong StartHeight { get; set; }
        public ulong EndHeight { get; set; }
        public List<IBlock> Blocks { get; set; }
        public Hash FirstBlockHash { get; set; }
        public Hash ForkHash { get; set; }
    }
}