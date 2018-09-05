// ReSharper disable once CheckNamespace
namespace AElf.ChainController
{
    public enum ValidationResult
    {
        // Should abandon: < 100
        AlreadyExecuted = 1,
        IncorrectTransactionMerkleTreeRoot = 10,
        IncorrectStateMerkleTreeRoot,
        IncorrectPreviousBlockHash,

        // Should store: < 1000
        LowerHeight = 1001,
        HeigherHeight,
        /*
        OrphanBlock = 2,
        InvalidBlock = 3,
        // Block height incontinuity, need other blocks
        Pending = 5,
        InvalidTimeslot = 7,
        FailedToCheckConsensusInvalidation = 8,
        FailedToGetBlockByHeight = 9,
        FailedToCheckChainContextInvalidation = 10,
        */
        
        // Should apply
        Success = 100001
    }
}