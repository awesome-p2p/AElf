using System;
using AElf.ChainController;

namespace AElf.Miner.Miner
{
    public class BlockExecutionResult
    {
        public ValidationResult ValidationResult { get; }
        public ExecutionResult ExecutionResult { get; }

        public bool IsSuccess =>
            ValidationResult == ValidationResult.Success && ExecutionResult == ExecutionResult.Success;

        public Exception ExecutionException { get; set; }

        public BlockExecutionResult(ExecutionResult executionResult, ValidationResult validationResult)
        {
            ExecutionResult = executionResult;
            ValidationResult = validationResult;
        }

        public BlockExecutionResult(Exception e)
        {
            ExecutionException = e;
        }
    }
}