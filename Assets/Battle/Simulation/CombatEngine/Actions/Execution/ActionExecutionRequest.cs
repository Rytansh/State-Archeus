using Archeus.Battle.Data.Actions;
using Unity.Entities;

namespace Archeus.Battle.Buffers.Actions
{
    public struct ActionExecutionRequest : IBufferElementData
    {
        public Entity Source;
        public Entity PrimaryTarget;

        public CharacterActionType CharacterAction;
    }
}
