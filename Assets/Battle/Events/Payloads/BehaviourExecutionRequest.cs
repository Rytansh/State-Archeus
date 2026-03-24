using Unity.Entities;

namespace DBUS.Battle.Components.Events
{
    public struct BehaviourExecutionRequest : IBufferElementData
    {
        public int BehaviourIndex;
        public int TriggerIndex;
        public int Priority;
        public Entity Owner;
        public BattleEvent SourceEvent;
        public int RegistrationIndex;
    }
}
