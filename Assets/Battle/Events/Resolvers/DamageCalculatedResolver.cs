using System;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Events.Runtime;
using Unity.Entities;

namespace Archeus.Battle.Events.Resolvers
{
    public static class DamageCalculatedResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt)
        {
            Entity attacker = evt.Source;
            Entity target = evt.Target;

            float inflictedDamage = evt.Payload.Damage.FinalDamage;

            //apply multipliers, modifiers, etc all to inflictedDamage.
            inflictedDamage *= evt.Payload.Damage.AttackMultiplier;
            
            ctx.ChainBuffer.Add(new ChainedBattleEvent
            {
                Event = new BattleEvent
                {
                    Type = BattleEventType.DamageMitigated,
                    Scope = evt.Scope,
                    Source = attacker,
                    Target = target,
                    Payload = new EventPayload
                    {
                        Damage = new DamagePayload
                        {
                            AttackMultiplier = evt.Payload.Damage.AttackMultiplier,
                            BaseDamage = evt.Payload.Damage.BaseDamage,
                            FinalDamage = inflictedDamage
                        }
                    }
                }
            });
        }
    }
}