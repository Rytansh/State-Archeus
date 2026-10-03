using Unity.Entities;

[DisableAutoCreation]
[UpdateInGroup(typeof(BattleRootGroup))]
[UpdateAfter(typeof(BattleSetupGroup))]
public partial class BattleSimulationGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[UpdateInGroup(typeof(BattleSimulationGroup))]
public partial class TurnFlowGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[UpdateInGroup(typeof(BattleSimulationGroup))]
public partial class VMSystemGroup : ComponentSystemGroup { }
