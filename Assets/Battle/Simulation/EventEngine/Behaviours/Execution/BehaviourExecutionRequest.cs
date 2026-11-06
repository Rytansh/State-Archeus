using Archeus.Battle.Events.Context;
using Unity.Entities;

namespace Archeus.Battle.Buffers.VM
{
    public struct BehaviourExecutionRequest : IBufferElementData
    {
        public int BehaviourIndex;
        public int TriggerIndex;
        public int Priority;
        public int RegistrationIndex;
        public bool IsActionProgram;
        public int ProgramIndex;

        public Entity Owner;
        public uint OwnerRuntimeID;
        public Entity Source;
        public Entity Target;

        public EventEmissionContext EmissionContext;
    }
}
