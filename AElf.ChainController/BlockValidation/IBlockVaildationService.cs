using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;

// ReSharper disable once CheckNamespace
namespace AElf.ChainController
{
    public interface IBlockVaildationService
    {
        Task<SyncSuggestion> ValidateBlockAsync(IBlock block, IChainContext context, ECKeyPair keyPair);
    }
}