using Unity.Entities;

[DisableAutoCreation]
[UpdateInGroup(typeof(BattleRootGroup))]
public partial class BattleSetupGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[UpdateInGroup(typeof(BattleSetupGroup))]
public partial class BattleCreationGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[UpdateInGroup(typeof(BattleSetupGroup))]
[UpdateAfter(typeof(BattleCreationGroup))]
public partial class BattleInitialisationGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[UpdateInGroup(typeof(BattleSetupGroup))]
[UpdateAfter(typeof(BattleInitialisationGroup))]
public partial class BattleSpawningGroup : ComponentSystemGroup { }
