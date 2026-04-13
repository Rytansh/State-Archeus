using Unity.Entities;

namespace Archeus.Battle.Components.Combat
{
    public struct PlayerHand: IComponentData
    {
        public int Current;
    }

    public struct MaxHandSize: IComponentData
    {
        public int Value;
    }
}