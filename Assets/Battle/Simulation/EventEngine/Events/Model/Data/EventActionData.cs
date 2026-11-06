using Archeus.Battle.Data.Actions;
using Unity.Entities;

namespace Archeus.Battle.Events.Context
{
    public struct EventActionData
    {
        public const uint InvalidExecutionID = 0;
        public const ushort NoActionResultGroup = ushort.MaxValue;
        public const ushort NoHit = ushort.MaxValue;

        public uint ActionExecutionID;
        public uint ActionDefinitionID;
        public uint QueueActionID;
        public uint ContextID;
        public uint CausingExecutionID;
        public Entity InheritedSource;
        public CharacterActionType ActionType;
        public ushort ActionResultGroupIndex;
        public ushort HitIndex;

        public bool HasActionContext => ActionExecutionID != InvalidExecutionID;

        public bool HasActionResultGroup =>
            HasActionContext && ActionResultGroupIndex != NoActionResultGroup;
    }
}
