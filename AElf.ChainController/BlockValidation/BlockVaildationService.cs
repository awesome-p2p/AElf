using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;

// ReSharper disable once CheckNamespace
namespace AElf.ChainController
{
    public class BlockVaildationService: IBlockVaildationService
    {
        private readonly IEnumerable<IValidationFilter> _filters;

        public BlockVaildationService(IEnumerable<IValidationFilter> filters)
        {
            _filters = filters;
        }

        public async Task<SyncSuggestion> ValidateBlockAsync(IBlock block, IChainContext context,
            ECKeyPair keyPair)
        {
            var results = new List<ValidationResult>();
            foreach (var filter in _filters)
            {
                var result = await filter.ValidateBlockAsync(block, context, keyPair);
                if ((int) result < 100)
                {
                    return SyncSuggestion.Abandon;
                }

                results.Add(result);
            }

            return GenerateSuggestion(results);
        }

        public SyncSuggestion GenerateSuggestion(List<ValidationResult> results)
        {
            if (results.All(r => r == ValidationResult.Success))
            {
                return SyncSuggestion.Apply;
            }
            
            return SyncSuggestion.Store;
        }
    }


}