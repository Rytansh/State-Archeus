using Unity.Entities;
using Archeus.Battle.VM.Programs;
using Archeus.Content.Lookup;

namespace Archeus.Content.Blobs
{
    public struct EffectBlob : IHasID
    {
        public uint ID;
        public uint GetID() => ID;
        public BlobArray<int> BehaviourIndices;
    }
}
