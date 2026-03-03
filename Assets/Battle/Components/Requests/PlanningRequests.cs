using Unity.Entities;

namespace DBUS.Battle.Components.Requests
{
    public struct EndPlanningRequest : IComponentData
    {
        public Entity Player;
    }
    public struct PlaceCardRequest : IComponentData
    {
        public Entity Player;
    }
    public struct PlayActionRequest : IComponentData
    {
        public Entity Player;
    }
}


