using Unity.Entities;

namespace Archeus.Battle.Components.Stats
{
    public struct CharacterStats: IComponentData
    {
        public float Attack;
        public float Defense;
        public float MaxHealth; 

        public float CritRATE;
        public float CritDMG;
    }
    //holds the base value of stats, modifiers are added to these to change them mid battle - NOT modified directly

    public struct SkillStats: IComponentData
    {
        public float Attack;
        public float Defense;
        public float Health; //okay to store here as it will never be directly modified
    }

}