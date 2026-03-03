using Unity.Entities;

public struct BattleEvent
{
    public BattleEventType Type;

    public Entity Source;
    public Entity Target;

    public EventPayload Payload;
}
