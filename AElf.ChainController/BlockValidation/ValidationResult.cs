// ReSharper disable once CheckNamespace
namespace AElf.ChainController
{
    public enum ValidationResult
    {
        // Should abandon: < 100
        AlreadyExecuted = 1,
        IncorrectTransactionMerkleTreeRoot = 10,
        IncorrectPreviousBlockHash,
        OutOfDate,

        // Should store: < 1000
        LowerHeight = 1001,
        HigherHeight,
        InvalidTimeSlot,
        
        // Should apply
        Success = 100001,
        FailedToValidate
    }
}