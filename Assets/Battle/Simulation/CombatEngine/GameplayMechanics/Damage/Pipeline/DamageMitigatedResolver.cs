using System;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Data.Effects;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Events.Context;
using Archeus.Battle.Stats;
using Unity.Entities;
using Archeus.Battle.Events.Factory;

namespace Archeus.Battle.Events.Resolvers
{
    public static class DamageMitigatedResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt, in EventEmissionContext emissionContext)
        {
            Entity attacker = evt.Source;
            Entity target = evt.Target;

            float finalDamageToTarget = evt.Payload.Damage.FinalDamage;

            float finalDefense = StatResolver.Resolve(target, StatType.Defense, ref ctx);
            //apply damage reduction, modifiers etc all to finalDamageToTarget.
            finalDamageToTarget = Math.Max(1f, finalDamageToTarget - finalDefense);
            
            BattleEvent damageResolvedEvent = new BattleEvent
            {
                Type = BattleEventType.DamageResolved,
                Scope = evt.Scope,
                Source = attacker,
                Target = target,
                Payload = new EventPayload
                {
                    Damage = new DamagePayload
                    {
                        AttackMultiplier = evt.Payload.Damage.AttackMultiplier,
                        BaseDamage = evt.Payload.Damage.BaseDamage,
                        FinalDamage = finalDamageToTarget,
                        DidCrit = evt.Payload.Damage.DidCrit,
                        CritMultiplier = evt.Payload.Damage.CritMultiplier
                    }
                },
                StructuralData = evt.StructuralData
            };

            BattleEventEmitter.EmitContinuationEvent(damageResolvedEvent, ref ctx.ChainedEventQueue, in emissionContext);

        }
    }
}