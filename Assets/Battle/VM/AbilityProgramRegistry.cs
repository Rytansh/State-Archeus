using Unity.Entities;
using System.Collections.Generic;
using DBUS.Battle.VM.Data;

namespace DBUS.Battle.VM.Systems
{
    public static class AbilityProgramRegistry
    {
        private static Dictionary<(int behaviourIndex, int triggerIndex), BlobAssetReference<AbilityProgram>> programs = new();

        public static void Register(int behaviourIndex, int triggerIndex, BlobAssetReference<AbilityProgram> program)
        {
            programs[(behaviourIndex, triggerIndex)] = program;
            Logging.System($"Registered program for ({behaviourIndex}, {triggerIndex})");
        }

        public static BlobAssetReference<AbilityProgram> Get(int behaviourIndex, int triggerIndex)
        {
            return programs[(behaviourIndex, triggerIndex)];
        }
    }
}