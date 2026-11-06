using Archeus.Battle.Data.Actions;
using Unity.Entities;

namespace Archeus.Battle.Buffers.Actions
{
    // Retained only for serialized-schema compatibility. Runtime scheduling consumes the active
    // execution context's PlannedAction buffer; no production system reads this legacy buffer.
    public struct ActionExecutionRequest : IBufferElementData
    {
        public Entity Source;
        public Entity PrimaryTarget;

        public CharacterActionType CharacterAction;
    }
}
