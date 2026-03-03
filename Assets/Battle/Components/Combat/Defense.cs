using Unity.Entities;

namespace DBUS.Battle.Components.Combat
{
    public struct CurrentDefense: IComponentData
    {
        public int Value;
    }
    // holds the current defense of a character during combat.
}
