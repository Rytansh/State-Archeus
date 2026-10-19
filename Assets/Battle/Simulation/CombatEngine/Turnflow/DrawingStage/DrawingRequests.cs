using Unity.Entities;

namespace Archeus.Battle.Components.Requests
{
    public struct DrawCardsRequest : IComponentData
    {
        public Entity Player;
    }

    public struct RetreatCardRequest : IComponentData
    {
        public Entity Player;
        public Entity CardToRetreat;
    }

    public struct EndDrawingRequest : IComponentData
    {
        public Entity Player;
    }
}
