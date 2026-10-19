using Unity.Entities;

namespace Archeus.Battle.Components.Turns
{
    public struct ActiveTurnPlayer : IComponentData
    {
        public Entity Player;
    }
}
