using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Kernel.Types;
using AElf.Kernel;

namespace AElf.ChainController
{
    public interface IChainContextService
    {
        Task<IChainContext> GetChainContextAsync(Hash chainId);

        List<PendingBlock> PendingBlocks { get; set; }
        List<PendingBlock> PendingForkBlocks { get; set; }

        void AddPendingBlock(PendingBlock pendingBlock);
        void RemovePendingBlock(PendingBlock pendingBlock);

    }
}