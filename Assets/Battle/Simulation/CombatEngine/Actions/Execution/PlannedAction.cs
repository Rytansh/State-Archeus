using Archeus.Battle.Data.Actions;
using Unity.Entities;

public enum PlannedActionType : byte
{
    CharacterAction,
    ApplySkill,
}

public struct PlannedAction : IBufferElementData
{
    public PlannedActionType ActionType;
    public Entity SubmittedByPlayer;
    public Entity Source;
    public Entity PrimaryTarget;
    public Entity SkillCard;
    public CharacterActionType CharacterAction;
}
