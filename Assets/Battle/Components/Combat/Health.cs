using Unity.Entities;

namespace DBUS.Battle.Components.Combat
{
    public struct MaxHealth : IComponentData
    {
        public int Value;
    }
    // holds the max HP of a character at the beginning of combat.

    public struct CurrentHealth : IComponentData
    {
        public int Value;
    }
    // holds the current HP of a character during combat.
}
