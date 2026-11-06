using Archeus.Battle.Data.Actions;
using Archeus.Battle.Scheduling;
using Unity.Entities;

public enum PlannedActionType : byte
{
    CharacterAction,
    ApplySkill,
}

public struct PlannedAction : IBufferElementData, IStableQueuedAction
{
    public uint QueueActionID;
    public uint ContextID;
    public uint DefinitionID;
    public ActionOrigin Origin;
    public ActionTargetMode TargetMode;
    public uint CausingExecutionID;
    public Entity InheritedSource;
    public int ReservedAP;
    public uint StableID => QueueActionID;
    public PlannedActionType ActionType;
    public Entity SubmittedByPlayer;
    public Entity Source;
    public Entity PrimaryTarget;
    public Entity SkillCard;
    public CharacterActionType CharacterAction;
}
