using Unity.Entities;

namespace DBUS.Battle.Components.Events
{
    public struct BehaviorExecutionRequest : IBufferElementData
    {
        public int BehaviourID;
        public int Priority;
        public Entity Owner;
        public BattleEvent SourceEvent;
        public int RegistrationIndex;
    }
}
