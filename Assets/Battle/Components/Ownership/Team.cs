using Unity.Entities;

namespace Archeus.Battle.Components.Ownership
{
    public struct Team : IComponentData
    {
        public BattleSide Side;
    }

    public enum BattleSide : byte
    {
        SideA,
        SideB,
    }

    public enum SideDescription : byte
    {
        Ally,
        Enemy,
    }
}
