using Unity.Entities;

namespace DBUS.Battle.Components.Determinism
{
    public struct BattleID: IComponentData
    {        
        public ulong Value;
    }
}