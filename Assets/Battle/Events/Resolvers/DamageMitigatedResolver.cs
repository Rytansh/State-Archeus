using System;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Events.Runtime;
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

            //apply damage reduction, modifiers etc all to finalDamageToTarget.
            finalDamageToTarget -= ctx.StatsLookup[target].Defense;
            
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
                            FinalDamage = finalDamageToTarget
                        }
                    }
                }
            });
        }
    }
}