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
    public static class DamageConfirmedResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt, in EventEmissionContext emissionContext)
        {
            Entity attacker = evt.Source;
            Entity target = evt.Target;

            float baseDamage = StatResolver.Resolve(attacker, StatType.Attack, ref ctx);

            BattleEvent damageCalaculatedEvent = new BattleEvent
            {
                Type = BattleEventType.DamageCalculated,
                Scope = evt.Scope,
                Source = attacker,
                Target = target,
                Payload = new EventPayload
                {
                    Damage = new DamagePayload
                    {
                        AttackMultiplier = evt.Payload.Damage.AttackMultiplier,
                        BaseDamage = baseDamage,
                        FinalDamage = baseDamage
                    }
                },
                StructuralData = evt.StructuralData
            };

            BattleEventEmitter.EmitContinuationEvent(damageCalaculatedEvent, ref ctx.ChainedEventQueue, in emissionContext);

        }
    }
}