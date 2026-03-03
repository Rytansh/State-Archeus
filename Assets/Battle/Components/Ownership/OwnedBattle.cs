using Unity.Entities;

namespace DBUS.Battle.Components.Ownership
{
    public struct OwnedBattle : IComponentData
    {
        public Entity Battle;
    }
    
}
