using System;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Data.Effects;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Context;
using Archeus.Battle.Events.Factory;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Presentation.Factory;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Stats;
using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Battle.Events.Resolvers
{
    public static class DamageResolvedResolver
    {
        public static void Resolve(
            ref BattleContext ctx,
            BattleEvent evt,
            in EventEmissionContext emissionContext
        )
        {
            Entity target = evt.Target;

            CurrentHealth hp = ctx.HealthLookup[target];
            float oldHp = hp.Value;

            float damageToApply = evt.Payload.Damage.FinalDamage;

            // Apply the damage
            hp.Value = Math.Max(0f, hp.Value - damageToApply);
            ctx.HealthLookup[target] = hp;

            float maxHealth = StatResolver.Resolve(target, StatType.MaxHealth, ref ctx);
            float hpPercent = hp.Value / maxHealth * 100f;

            bool didCrit = evt.Payload.Damage.DidCrit;
            if (didCrit)
            {
                Logging.Info(
                    LogCategory.Combat,
                    $"[Health Update] CRITICAL HIT!! Entity {target.Index}: "
                        + $"{oldHp} -> {hp.Value} (-{damageToApply}) | "
                        + $"Current HP: {hpPercent:F1}%"
                );
            }
            else
            {
                Logging.Info(
                    LogCategory.Combat,
                    $"[Health Update] Entity {target.Index}: "
                        + $"{oldHp} -> {hp.Value} (-{damageToApply}) | "
                        + $"Current HP: {hpPercent:F1}%"
                );
            }

            uint sourceRuntimeID = ctx.CardRuntimeIDLookup[evt.Source].Value;
            uint targetRuntimeID = ctx.CardRuntimeIDLookup[evt.Target].Value;

            PresentationHitPayload hitPayload = new PresentationHitPayload
            {
                Damage = damageToApply,
                IsCrit = didCrit,
            };

            PresentationFactContext factContext = new PresentationFactContext
            {
                BattleRuntimeID = ctx.BattleID,

                SourceRuntimeID = sourceRuntimeID,
                TargetRuntimeID = targetRuntimeID,

                GroupID = evt.StructuralData.GroupID,
                Generation = evt.StructuralData.Generation,

                ActionDefinitionID = 0,
                ActionExecutionID = evt.ActionData.ActionExecutionID,
                ActionResultIndex = evt.ActionData.ActionResultGroupIndex,
                HitIndex = evt.ActionData.HitIndex,
            };

            PresentationFactEmitter.EmitDamageAppliedFact(
                hitPayload,
                factContext,
                ctx.PresentationFactQueue,
                ctx.PresentationSequenceCounter
            );

            BattleEvent damageAppliedEvent = new BattleEvent
            {
                Type = BattleEventType.DamageApplied,
                Scope = evt.Scope,
                Source = evt.Source,
                Target = target,
                Payload = new EventPayload
                {
                    Damage = new DamagePayload
                    {
                        AttackMultiplier = evt.Payload.Damage.AttackMultiplier,
                        BaseDamage = evt.Payload.Damage.BaseDamage,
                        FinalDamage = evt.Payload.Damage.FinalDamage,
                        DidCrit = evt.Payload.Damage.DidCrit,
                        CritMultiplier = evt.Payload.Damage.CritMultiplier,
                    },
                },
                StructuralData = evt.StructuralData,
            };

            BattleEvent damageReceivedEvent = new BattleEvent
            {
                Type = BattleEventType.DamageReceived,
                Scope = evt.Scope,
                Source = evt.Source,
                Target = target,
                Payload = new EventPayload
                {
                    Damage = new DamagePayload
                    {
                        AttackMultiplier = evt.Payload.Damage.AttackMultiplier,
                        BaseDamage = evt.Payload.Damage.BaseDamage,
                        FinalDamage = evt.Payload.Damage.FinalDamage,
                        DidCrit = evt.Payload.Damage.DidCrit,
                        CritMultiplier = evt.Payload.Damage.CritMultiplier,
                    },
                },
                StructuralData = evt.StructuralData,
            };

            BattleEventEmitter.EmitContinuationEvent(
                damageAppliedEvent,
                ref ctx.ChainedEventQueue,
                in emissionContext
            );

            BattleEventEmitter.EmitContinuationEvent(
                damageReceivedEvent,
                ref ctx.ChainedEventQueue,
                in emissionContext
            );
        }
    }
}
