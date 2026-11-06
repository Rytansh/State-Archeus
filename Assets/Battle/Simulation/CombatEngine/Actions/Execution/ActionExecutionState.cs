using Archeus.Battle.Data.Actions;
using Archeus.Battle.Scheduling;
using Unity.Entities;

namespace Archeus.Battle.Buffers.Actions
{
    public struct ActionExecutionState : IBufferElementData
    {
        public uint ActionExecutionID;
        public uint QueueActionID;
        public uint ContextID;
        public Entity Context;
        public PlannedAction Action;
        public ActionOutcome Outcome;
        public bool ProgramQueued;

        public ushort NextResultGroupIndex;
        public ushort NextHitIndex;

        public Entity Source;
        public Entity PrimaryTarget;
        public CharacterActionType ActionType;
    }
}
