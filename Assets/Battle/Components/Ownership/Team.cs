using Unity.Entities;

namespace Archeus.Battle.Components.Ownership
{
    public struct Team : IComponentData
    {
        public BattleSide Side;
    }

    public enum BattleSide
    {
        Ally,
        Enemy
    }
    
}
