using System;
using AElf.ChainController;

namespace AElf.Miner.Miner
{
    public class BlockExecutionResult
    {
        public SyncSuggestion SyncSuggestion { get; }
        public ExecutionResult ExecutionResult { get; }

        public bool IsSuccess =>
            SyncSuggestion != SyncSuggestion.Abandon && ExecutionResult == ExecutionResult.Success;

        public Exception ExecutionException { get; set; }

        public BlockExecutionResult(ExecutionResult executionResult, SyncSuggestion syncSuggestion)
        {
            ExecutionResult = executionResult;
            SyncSuggestion = syncSuggestion;
        }

        public BlockExecutionResult(Exception e)
        {
            ExecutionException = e;
        }
    }
}