namespace Archeus.Battle.Events.Context
{
    public struct EventEmissionContext
    {
        public EventStructuralData StructuralData;
        public EventActionData ActionData;
        public EventExecutionData ExecutionData;
        public uint CurrentFrameID;
    }
}
