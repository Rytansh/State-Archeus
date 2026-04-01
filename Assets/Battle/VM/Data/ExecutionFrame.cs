using Unity.Entities;
using Unity.Collections;

namespace DBUS.Battle.VM.Data
{
    public struct AbilityExecutionFrame
    {
        public Entity Source;
        public Entity Target;

        public int ProgramIndex;
        public int InstructionPointer;
        public FixedList64Bytes<float> Stack;
    }
}
