using System;
using AElf.ChainController;

namespace AElf.Miner.Miner
{
    public class BlockExecutionResult
    {
        public ValidationResult ValidationResult { get; }
        public ExecutionResult Executed { get; }

        public Exception ExecutionException { get; set; }

        public BlockExecutionResult(ExecutionResult executionResult, ValidationResult validationResult)
        {
            Executed = executionResult;
            ValidationResult = validationResult;
        }

        public BlockExecutionResult(Exception e)
        {
            ExecutionException = e;
        }
    }
}