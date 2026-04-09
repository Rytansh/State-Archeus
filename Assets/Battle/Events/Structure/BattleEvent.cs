using Unity.Entities;

public struct BattleEvent : IBufferElementData
{
    public BattleEventType Type;
    public BattleEventScope Scope;

    public Entity Source;
    public Entity Target;
    public EventPayload Payload;
}

public enum BattleEventScope : byte
{
    Targeted,
    Global
}
