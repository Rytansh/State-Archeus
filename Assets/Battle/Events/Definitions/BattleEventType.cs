namespace Archeus.Battle.Events.Definitions
{
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
        DamageConfirmed,
        DamageCalculated,
        DamageMitigated,
        DamageResolved,
        DamageApplied,

        //(Effect Related Events)

        EffectApplicationRequested,
        EffectApplicationResolved,
        EffectApplied,
        EffectRemoved,
        EffectExpired,

        //(Status Related Events)
        EntityKilled,
        ReactionTriggered
    }

    public enum BattleEventPhase
    {
        PreResolution,
        Resolution,
        PostResolution
    }
    public enum EventValueType
    {
        DamageBase,
        DamageFinal,
        DamageMultiplier
    }

}