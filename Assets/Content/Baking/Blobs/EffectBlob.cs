using Unity.Entities;
using Archeus.Content.Lookup;
using Archeus.Battle.Data.Effects;

namespace Archeus.Content.Blobs
{
    public struct EffectBlob : IHasID
    {
        public uint ID;
        public uint GetID() => ID;

        public StackingBehaviour StackBehaviour;
        public BlobArray<StatModifier> StatModifiers;
        public BlobArray<int> BehaviourIndices;
    }
}
