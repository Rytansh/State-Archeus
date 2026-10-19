using Unity.Entities;

[DisableAutoCreation]
[UpdateInGroup(typeof(TurnFlowGroup))]
public partial class TurnStartGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[UpdateInGroup(typeof(TurnFlowGroup))]
[UpdateAfter(typeof(TurnStartGroup))]
public partial class DrawingStageGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[UpdateInGroup(typeof(TurnFlowGroup))]
[UpdateAfter(typeof(DrawingStageGroup))]
public partial class PlanningStageGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[UpdateInGroup(typeof(TurnFlowGroup))]
[UpdateAfter(typeof(PlanningStageGroup))]
public partial class AttackingStageGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[UpdateInGroup(typeof(TurnFlowGroup))]
[UpdateAfter(typeof(AttackingStageGroup))]
public partial class TurnEndGroup : ComponentSystemGroup { }
