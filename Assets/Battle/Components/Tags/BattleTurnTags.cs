using Unity.Entities;

namespace Archeus.Battle.Components.Tags
{
    public struct BattleTurnStartTag: IComponentData {}
    public struct BattleTurnStartCompleteTag: IComponentData {}

    public struct BattleTurnEndTag: IComponentData {}
    public struct BattleTurnEndCompleteTag: IComponentData {}
}