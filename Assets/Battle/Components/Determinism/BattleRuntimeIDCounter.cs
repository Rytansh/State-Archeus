using Unity.Entities;

namespace DBUS.Battle.Components.Determinism
{
    public struct BattleRuntimeIDCounter: IComponentData
    {        
        public uint NextID;
    }
}