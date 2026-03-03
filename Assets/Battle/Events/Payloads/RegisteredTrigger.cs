using Unity.Entities;

namespace DBUS.Battle.Components.Events
{
    public struct RegisteredTrigger : IBufferElementData
    {
        public BattleEventType EventType;
        public int Priority;
        public Entity Owner;
        public int BehaviourID;
        public int RegistrationIndex;
    }
}
