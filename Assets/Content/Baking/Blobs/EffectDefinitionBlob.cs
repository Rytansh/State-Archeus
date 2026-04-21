using Unity.Entities;
using Archeus.Game.Stats;
using Archeus.Battle.Effects;
using Archeus.Content.Lookup;

namespace Archeus.Content.Blobs
{
    public struct EffectDefinitionBlob : IHasID
    {
        public uint EffectID;
        public uint GetID() => EffectID;
        public int Duration;
        public StackingType StackingType;
        public BlobArray<StatModifierBlob> Modifiers;
    }

    public struct StatModifierBlob
    {
        public StatType StatType;
        public StatModifierType ModifierType;
        public float Value;
    }
}
