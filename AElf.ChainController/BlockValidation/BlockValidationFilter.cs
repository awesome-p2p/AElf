using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;

// ReSharper disable once CheckNamespace
namespace AElf.ChainController
{
    /// <summary>
    /// Validate the tx merkle tree root.
    /// </summary>
    public class BlockValidationFilter : IValidationFilter
    {
        public Task<ValidationResult> ValidateBlockAsync(IBlock block, IChainContext context, ECKeyPair keyPair)
        {
            return Task.FromResult(block.Body.CalculateMerkleTreeRoot() != block.Header.MerkleTreeRootOfTransactions
                ? ValidationResult.IncorrectTransactionMerkleTreeRoot
                : ValidationResult.Success);
        }
    }
}