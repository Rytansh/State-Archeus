using Unity.Entities;

[UpdateInGroup(typeof(TurnFlowGroup))]
public partial class TurnStartGroup : ComponentSystemGroup {}


[UpdateInGroup(typeof(TurnFlowGroup))]
[UpdateAfter(typeof(TurnStartGroup))]
public partial class DrawingStageGroup : ComponentSystemGroup {}


[UpdateInGroup(typeof(TurnFlowGroup))]
[UpdateAfter(typeof(DrawingStageGroup))]
public partial class PlanningStageGroup : ComponentSystemGroup {}


[UpdateInGroup(typeof(TurnFlowGroup))]
[UpdateAfter(typeof(PlanningStageGroup))]
public partial class AttackingStageGroup : ComponentSystemGroup {}


[UpdateInGroup(typeof(TurnFlowGroup))]
[UpdateAfter(typeof(AttackingStageGroup))]
public partial class TurnEndGroup : ComponentSystemGroup {}
