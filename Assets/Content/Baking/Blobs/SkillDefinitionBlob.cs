using Unity.Entities;
using Archeus.Content.Lookup;

namespace Archeus.Content.Blobs
{
    public struct SkillDefinitionBlob : IHasID
    {
        public uint ID;
        public uint GetID() => ID;
        public byte Rarity;
        public byte Speciality; 
        public int Duration;

        public SkillBlobBaseStats SkillBlobBaseStats;

        public uint NormalAbilityID;
        public uint DelayAndImprovementAbilityID;
        public BlobArray<uint> BehaviourIDs;
    }

    public struct SkillBlobBaseStats
    {
        public float Attack;
        public float Defense;
        public float Health;
    }

    public struct SkillDatabaseBlob
    {
        public BlobArray<SkillDefinitionBlob> Skills;
    }

    public struct SkillDatabaseComponent : IComponentData
    {
        public BlobAssetReference<SkillDatabaseBlob> Blob;
    }
}
