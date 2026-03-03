public enum BattleEventType : ushort
{
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
    DamageCalculated,
    DamageApplied,

    //(Status Related Events)
    BuffApplied,
    BuffRemoved,
    EntityKilled,
    ReactionTriggered
}