using System;
using System.Threading.Tasks;
using AElf.Common.Attributes;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using NLog;

// ReSharper disable once CheckNamespace
namespace AElf.ChainController
{
    [LoggerName(nameof(ChainContextValidationFilter))]
    public class ChainContextValidationFilter : IValidationFilter
    {
        private readonly IChainService _chainService;
        private readonly ILogger _logger;

        public ChainContextValidationFilter(IChainService chainService, ILogger logger)
        {
            _chainService = chainService;
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateBlockAsync(IBlock block, IChainContext context, ECKeyPair keyPair)
        {
            try
            {
                /*
                    1' block height
                    2' previous block hash
                */
    
                var index = block.Header.Index;
                var previousBlockHash = block.Header.PreviousBlockHash;
    
                // return success if genesis block
                /*if (index == 0 && previousBlockHash.Equals(Hash.Zero))
                    return TxInsertionAndBroadcastingError.Valid;*/
    
                var currentChainHeight = context.BlockHeight;
                var currentPreviousBlockHash = context.BlockHash;

                if (index < currentChainHeight - (ulong) Globals.BlockNumberOfEachRound)
                {
                    return ValidationResult.OutOfDate;
                }

                // other block needed before this one
                if (index > currentChainHeight + 1)
                {
                    _logger?.Trace("Received block index:" + index);
                    _logger?.Trace("Current chain height:" + currentChainHeight);
                    
                    return ValidationResult.HigherHeight;
                }
                
                // can be added to chain
                if (previousBlockHash == Hash.Genesis ||  index == currentChainHeight + 1)
                {
                    if (currentPreviousBlockHash.Equals(previousBlockHash))
                        return ValidationResult.Success;

                    _logger?.Trace("context.BlockHash:" + currentPreviousBlockHash.ToHex());
                    _logger?.Trace("block.Header.PreviousBlockHash:" + previousBlockHash.ToHex());

                    return ValidationResult.IncorrectPreviousBlockHash;
                }
                
                if (index <= currentChainHeight)
                {
                    var blockchain = _chainService.GetBlockChain(block.Header.ChainId);
                    var b = await blockchain.GetBlockByHeightAsync(index);
                    return b.Header.GetHash().Equals(block.Header.GetHash())
                        ? ValidationResult.AlreadyExecuted
                        : ValidationResult.LowerHeight;
                }
                
                _logger?.Error("Incomplete validation scheme.");
                return ValidationResult.FailedToValidate;
            }
            catch (Exception e)
            {
                _logger?.Error(e, "Error while validating blocks.");
                return ValidationResult.FailedToValidate;
            }
        }
    }
}