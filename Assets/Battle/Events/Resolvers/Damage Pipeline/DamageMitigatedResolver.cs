using System;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Data.Effects;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Events.Runtime;
using Archeus.Battle.Stats;
using Unity.Entities;

namespace Archeus.Battle.Events.Resolvers
{
    public static class DamageMitigatedResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt)
        {
            Entity attacker = evt.Source;
            Entity target = evt.Target;

            float finalDamageToTarget = evt.Payload.Damage.FinalDamage;

            float finalDefense = StatResolver.Resolve(target, StatType.Defense, ref ctx);
            //apply damage reduction, modifiers etc all to finalDamageToTarget.
            finalDamageToTarget = Math.Max(1f, finalDamageToTarget - finalDefense);
            
            ctx.ChainBuffer.Add(new ChainedBattleEvent
            {
                Event = new BattleEvent
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
                    }
                }
            });
        }
    }
}