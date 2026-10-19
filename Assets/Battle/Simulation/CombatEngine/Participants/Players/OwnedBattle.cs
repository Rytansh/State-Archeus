using Unity.Entities;

namespace Archeus.Battle.Components.Ownership
{
    public struct OwnedBattle : IComponentData
    {
        public Entity Battle;
    }
    
}
