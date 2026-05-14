using Unity.Entities;
using Unity.Collections;

namespace Archeus.Battle.VM.Execution
{
    public struct AbilityExecutionFrame
    {
        public Entity BehaviourOwner;
        public Entity Source;
        public Entity Target;

        public int ProgramIndex;
        public int InstructionPointer;
        public FixedList64Bytes<float> Stack;
    }
}
