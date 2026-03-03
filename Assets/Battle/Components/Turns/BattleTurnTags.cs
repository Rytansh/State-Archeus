using Unity.Entities;

namespace DBUS.Battle.Components.Turns
{
    public struct BattleTurnStartTag: IComponentData {}
    public struct BattleTurnStartCompleteTag: IComponentData {}

    public struct BattleTurnEndTag: IComponentData {}
    public struct BattleTurnEndCompleteTag: IComponentData {}
}