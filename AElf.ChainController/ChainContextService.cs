using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Kernel;

namespace AElf.ChainController
{
    public class ChainContextService : IChainContextService
    {
        private readonly IChainService _chainService;
        private readonly IBlockCollection _blockCollection;

        public List<PendingBlock> PendingBlocks
        {
            get => _blockCollection.PendingBlocks;
            set => _blockCollection.PendingBlocks = value;
        }

        public List<PendingBlock> PendingForkBlocks
        {
            get => _blockCollection.PendingForkBlocks;
            set => _blockCollection.PendingForkBlocks = value;
        }
        
        public void AddPendingBlock(PendingBlock pendingBlock)
        {
            _blockCollection.AddPendingBlock(pendingBlock);
        }

        public void RemovePendingBlock(PendingBlock pendingBlock)
        {
            _blockCollection.RemovePendingBlock(pendingBlock);
        }

        public ChainContextService(IChainService chainService)
        {
            _chainService = chainService;
            _blockCollection = new BlockCollection();
        }

        public async Task<IChainContext> GetChainContextAsync(Hash chainId)
        {
            var blockchain = _chainService.GetBlockChain(chainId);
            IChainContext chainContext = new ChainContext
            {
                ChainId = chainId,
                BlockHash = await blockchain.GetCurrentBlockHashAsync()
            };
            if (chainContext.BlockHash != Hash.Genesis)
            {
                chainContext.BlockHeight = ((BlockHeader)await blockchain.GetHeaderByHashAsync(chainContext.BlockHash)).Index;
            }
            return chainContext;
        }
    }
}