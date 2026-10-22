using Archeus.Content.Lookup;
using Unity.Entities;

namespace Archeus.Content.Blobs
{
    public struct CharacterDefinitionBlob : IHasID
    {
        public uint ID;

        public uint GetID() => ID;

        public byte Rarity;
        public byte BattleType;
        public byte Speciality;
        public CharacterBlobBaseStats CharacterBlobBaseStats;

        public uint NormalAttackID;
        public uint SuperchargedAttackID;
        public uint FinalTrumpSkillID;
        public BlobArray<int> BehaviourIndices;
    }

    public struct CharacterBlobBaseStats
    {
        public float MaxHealth;
        public float Attack;
        public float Defense;

        public float CritDMG;
        public float CritRATE;

        public float SustainPOWER;
        public float DOTDMGBonus;

        public float Authority;
        public float Reactivity;
        public float Stability;
        public float Observation;
        public float Initiative;

        public float ALLDMGBonus;
    }
}
