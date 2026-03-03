using Unity.Entities;

namespace DBUS.Battle.Components.Determinism
{
    public struct BattleRNG: IComponentData
    {
        public ulong StateA;
        public ulong StateB;
    }

}
