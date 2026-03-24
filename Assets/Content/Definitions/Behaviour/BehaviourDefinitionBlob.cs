using Unity.Entities;

public struct BehaviourDefinitionBlob : IHasID
{
    public uint ID;
    public uint GetID() => ID;
    public BlobArray<BehaviourTriggerBlob> Triggers;
}

public struct BehaviourTriggerBlob
{
    public BattleEventType EventType;
    public uint VMProgramID;
    public BlobArray<EventConditionBlob> Conditions;
    public int Priority;
}

public struct EventConditionBlob
{
    public ConditionType Type;
    public float Value;
}