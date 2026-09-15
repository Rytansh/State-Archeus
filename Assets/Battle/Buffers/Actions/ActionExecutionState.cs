using Archeus.Battle.Data.Actions;
using Unity.Entities;

namespace Archeus.Battle.Buffers.Actions
{
    public struct ActionExecutionState : IBufferElementData
    {
        public uint ActionExecutionID;

        public ushort NextResultGroupIndex;
        public ushort NextHitIndex;

        public Entity Source;
        public Entity PrimaryTarget;
        public CharacterActionType ActionType;
    }
}
