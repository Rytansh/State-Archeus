namespace Archeus.Battle.VM.Execution
{
    public enum AbilityExecutionStatus : byte
    {
        Completed,
        Yielded,
        Aborted,
    }

    public struct AbilityExecutionResult
    {
        public AbilityExecutionStatus Status;
        public uint WaitingOperationID;

        public static AbilityExecutionResult Completed =>
            new AbilityExecutionResult
            {
                Status = AbilityExecutionStatus.Completed,
                WaitingOperationID = 0,
            };

        public static AbilityExecutionResult Aborted =>
            new AbilityExecutionResult
            {
                Status = AbilityExecutionStatus.Aborted,
                WaitingOperationID = 0,
            };

        public static AbilityExecutionResult Yielded(uint operationID)
        {
            return new AbilityExecutionResult
            {
                Status = AbilityExecutionStatus.Yielded,
                WaitingOperationID = operationID,
            };
        }
    }
}
