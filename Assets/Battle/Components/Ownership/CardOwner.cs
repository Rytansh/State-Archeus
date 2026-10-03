using Unity.Entities;

namespace Archeus.Battle.Components.Ownership
{
    public struct CardOwner : IComponentData
    {
        public Entity Player;
    }
}
