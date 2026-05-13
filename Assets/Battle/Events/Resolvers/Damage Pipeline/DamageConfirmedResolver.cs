using System;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Events.Runtime;
using Archeus.Battle.Stats;
using Unity.Entities;

namespace Archeus.Battle.Events.Resolvers
{
    public static class DamageConfirmedResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt)
        {
            Entity attacker = evt.Source;
            Entity target = evt.Target;

            float baseDamage = StatResolver.Resolve(attacker, StatType.Attack, ref ctx);

            ctx.ChainBuffer.Add(new ChainedBattleEvent
            {
                Event = new BattleEvent
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
                    }
                }
            });
        }
    }
}