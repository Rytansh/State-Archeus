using Unity.Entities;

[UpdateInGroup(typeof(BattleRootGroup))]
public partial class BattleSetupGroup : ComponentSystemGroup {}


[UpdateInGroup(typeof(BattleSetupGroup))]
public partial class BattleCreationGroup : ComponentSystemGroup {}


[UpdateInGroup(typeof(BattleSetupGroup))]
[UpdateAfter(typeof(BattleCreationGroup))]
public partial class BattleInitialisationGroup : ComponentSystemGroup {}


[UpdateInGroup(typeof(BattleSetupGroup))]
[UpdateAfter(typeof(BattleInitialisationGroup))]
public partial class BattleSpawningGroup : ComponentSystemGroup {}