using Unity.Entities;

namespace DBUS.Battle.Components.Turns
{
    public struct BattleDrawingCompleteTag: IComponentData {}
    public struct BattlePlanningCompleteTag: IComponentData {}
    public struct BattleAttackingCompleteTag: IComponentData {}
}