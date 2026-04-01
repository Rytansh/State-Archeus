using Unity.Entities;

namespace DBUS.Battle.VM.Data
{
    public struct AbilityProgram
    {
        public uint ID;
        public BlobArray<AbilityInstruction> Instructions;
    }
}
