using Unity.Entities;

namespace DBUS.Battle.Components.Events
{
    public struct BattleEventProcessingState : IComponentData
    {
        public bool IsProcessing;
    }
}
