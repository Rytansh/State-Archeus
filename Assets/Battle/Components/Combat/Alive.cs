using Unity.Entities;

namespace DBUS.Battle.Components.Combat
{
    public struct Alive: IComponentData{}
    // holds whether an entity is alive.

    public struct Dead: IComponentData{}
    // holds whether an entity is dead and should be removed from combat.
}
