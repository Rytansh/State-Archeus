using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.VM.Execution
{
    public struct AbilityExecutionFrame
    {
        public Entity BehaviourOwner;
        public Entity Source;
        public Entity Target;
        public FixedList512Bytes<Entity> Targets;

        public int ProgramIndex;
        public int InstructionPointer;
        public FixedList64Bytes<float> Stack;
        public int StepsExecuted;
    }
}
