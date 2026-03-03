using Unity.Entities;

namespace DBUS.Battle.Components.Combat
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