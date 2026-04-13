using Unity.Entities;

namespace Archeus.Battle.Buffers.VM
{
    public struct BehaviourExecutionRequest : IBufferElementData
    {
        public int BehaviourIndex;
        public int TriggerIndex;
        public int Priority;
        public int RegistrationIndex;

        public Entity Owner;
        public Entity Source;
        public Entity Target;
    }
}
