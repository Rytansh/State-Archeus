using Archeus.Battle.Components.Core;
using Unity.Entities;

public enum AttackingStageExecutionType
{
    NormalTurn,
    ExtraTurn,
    ExtraAction,
}

public enum AttackingStageExecutionStatus
{
    Created,
    Active,
    Suspended,
    Completed,
}

public struct AttackingStageExecutionContext : IComponentData
{
    public uint ContextID;

    public Entity Battle;
    public Entity ParentContext;
    public Entity ActingPlayer;
    public Entity ActingCharacter;

    public AttackingStageExecutionType ExecutionType;
    public AttackingStageExecutionStatus ExecutionStatus;
    public BattlePhase DeferredPhase;

    public int RemainingActionPoints;
    public int NextActionIndex;
}

public struct AttackingStageExecutionInstance : IComponentData
{
    public Entity Context;
}

public struct AttackingStageExecutionInstanceCounter : IComponentData
{
    public uint NextID;
}
