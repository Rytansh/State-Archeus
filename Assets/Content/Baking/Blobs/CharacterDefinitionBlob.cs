using Unity.Entities;
using Archeus.Content.Lookup;
using Archeus.Game.Stats;

namespace Archeus.Content.Blobs
{
    public struct CharacterDefinitionBlob : IHasID
    {
        public uint ID;   
        public uint GetID() => ID;
        public CharacterRarity Rarity;
        public BattleType BattleType;
        public Speciality Speciality;
        public CharacterBlobBaseStats CharacterBlobBaseStats;

        public uint NormalAttackID;   
        public uint SuperchargedAttackID;
        public uint FinalTrumpSkillID;
        public BlobArray<uint> BehaviourIDs;
    }

    public struct CharacterBlobBaseStats
    {
        public float MaxHealth;
        public float Attack;
        public float Defense;
        public float CritDMG;
        public float CritRATE;
        public float Reactivity;
        public float SustainPOWER;
        public float MagicalDMGBonus;
        public float PowerfulDMGBonus;
        public float TacticalDMGBonus;
        public float FlexibleDMGBonus;
        public float DOTDMGBonus;
        public float ALLDMGBonus;
    }
}
