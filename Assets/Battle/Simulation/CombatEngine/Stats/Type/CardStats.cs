using Unity.Entities;

namespace Archeus.Battle.Components.Stats
{
    public struct CharacterRuntimeStatBaseline : IComponentData
    {
        public float Attack;
        public float Defense;
        public float MaxHealth;

        public float CritRATE;
        public float CritDMG;

        public float DOTDMGBonus;
        public float Authority;
        public float Initiative;
        public float Stability;
        public float Observation;
        public float Reactivity;
        public float ALLDMGBonus;
    }

    //holds the base value of stats, modifiers are added to these to change them mid battle - NOT modified directly

    public struct SkillRuntimeStatBaseline : IComponentData
    {
        public float Attack;
        public float Defense;
        public float Health;
    }
}
