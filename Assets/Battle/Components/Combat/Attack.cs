using Unity.Entities;

namespace DBUS.Battle.Components.Combat
{
    public struct CurrentAttack : IComponentData
    {
        public int Value;
    }
    // holds the current attack of a character during combat.
}
