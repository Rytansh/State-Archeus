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
        DamageCalculated,
        DamageMitigated,
        DamageResolved,
        DamageApplied,

        //(Status Related Events)
        EffectApplicationRequested,
        EffectApplicationResolved,
        EffectApplied,
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