using Unity.Entities;

namespace DBUS.Battle.Components.Events
{
    public struct BattleEventBuffer : IBufferElementData
    {
        public BattleEvent Value;
    }
}
