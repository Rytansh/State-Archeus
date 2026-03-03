using Unity.Entities;

namespace DBUS.Battle.Components.Combat
{
    public struct Ally: IComponentData{}
    // holds whether an entity is an ally.

    public struct Enemy: IComponentData{}
    // holds whether an entity is an enemy.
}
