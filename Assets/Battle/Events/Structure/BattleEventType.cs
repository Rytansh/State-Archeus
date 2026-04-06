public enum BattleEventType : ushort
{
    TestEvent,
    //(Turn Related Events)
    TurnStarted,
    TurnEnded,
    PhaseStarted,
    PhaseEnded,

    //(Intent Related Events)
    ActionDeclared,
    AbilityActivated,
    CardPlaced,

    //(Resource Related Events)
    ResourceChanged,

    //(Damage Related Events)
    DamageRequested,
    DamageResolved,

    //(Status Related Events)
    BuffApplied,
    BuffRemoved,
    EntityKilled,
    ReactionTriggered
}

public enum BattleEventPhase
{
    PreResolution,
    Resolution,
    PostResolution
}