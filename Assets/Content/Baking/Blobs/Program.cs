using Unity.Entities;
using Archeus.Content.Lookup;
using Archeus.Battle.Data.VM;

namespace Archeus.Content.Blobs
{
    public struct AbilityProgram : IHasID
    {
        public uint ID;

        public uint GetID() => ID;
        public BlobArray<AbilityInstruction> Instructions;
    }
}
