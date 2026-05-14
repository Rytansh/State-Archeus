using Unity.Entities;

namespace Archeus.Battle.Components.Requests
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

    public struct CycleTargetRequest : IComponentData
    {
        public Entity Player;
        public int Direction;
    }

    public struct CycleCharacterRequest : IComponentData
    {
        public Entity Player;
        public int Direction;
    }
}


