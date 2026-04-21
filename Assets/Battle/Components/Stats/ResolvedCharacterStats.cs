using Unity.Entities;

namespace Archeus.Battle.Components.Stats
{
    public struct ResolvedCharacterStats: IComponentData
    {
        public float Attack;
        public float Defense;
        public float MaxHealth; 

        public float CritRATE;
        public float CritDMG;
    }
}
