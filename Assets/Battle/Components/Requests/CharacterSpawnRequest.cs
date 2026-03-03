using Unity.Entities;

namespace DBUS.Battle.Components.Requests
{
    public struct SpawnCharacterRequest : IComponentData
    {
        public Entity Battle;
        public int Slot;
        public int MaxHealth;
        public int Attack;
        public int Defense;
    }
}

