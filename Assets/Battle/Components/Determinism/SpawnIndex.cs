using Unity.Entities;

namespace DBUS.Battle.Components.Determinism
{
    public struct SpawnIndex: IComponentData
    {        
        public ulong Value;
    }
}