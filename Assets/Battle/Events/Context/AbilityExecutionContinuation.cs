using Archeus.Battle.VM.Execution;

namespace Archeus.Battle.Events.Context
{
    public struct AbilityExecutionContinuation
    {
        public AbilityExecutionFrame Frame;
        public int BehaviourStateIndex;
        public EventEmissionContext BaseEmissionContext;
        public uint ParentFrameID;
        public uint WaitingOperationID;
    }
}
