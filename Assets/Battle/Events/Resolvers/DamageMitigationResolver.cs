using System;
using Unity.Entities;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Events.Runtime;
using Archeus.Battle.Components.Stats;

namespace Archeus.Battle.Events.Resolvers
{
    public static class DamageMitigationResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt)
        {
            Entity target = evt.Target;

            if (!ctx.StatsLookup.HasComponent(target)) return;

            float targetDefense = evt.Payload.Damage.Snapshot.TargetDefense;
            
            float damageAfterMitigation = Math.Max(1f, evt.Payload.Damage.FinalDamage - targetDefense);

            ctx.ChainBuffer.Add(new ChainedBattleEvent
            {
                Event = new BattleEvent
                {
                    Type = BattleEventType.DamageResolved,
                    Source = evt.Source,
                    Target = target,
                    Payload = new EventPayload
                    {
                        Damage = new DamagePayload
                        {
                            AttackMultiplier = evt.Payload.Damage.AttackMultiplier,
                            BaseDamage = evt.Payload.Damage.BaseDamage,
                            FinalDamage = damageAfterMitigation,
                            Snapshot = evt.Payload.Damage.Snapshot
                        }
                    }
                }
            });
        }
    }
}