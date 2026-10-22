using Unity.Entities;

namespace Archeus.Battle.Components.Tags
{
    // turnflow associated tags //
    public struct BattleDrawingCompleteTag : IComponentData { }

    public struct BattlePlanningCompleteTag : IComponentData { }

    public struct BattleAttackingCompleteTag : IComponentData { }

    public struct BattleTurnStartCompleteTag : IComponentData { }

    public struct BattleTurnEndCompleteTag : IComponentData { }

    // player associated tags //
    public struct SkipDrawingStageTag : IComponentData { }

    public struct SkipPlanningStageTag : IComponentData { }

    public struct SkipAttackingStageTag : IComponentData { }
}
