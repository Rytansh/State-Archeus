using Unity.Entities;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Events.Runtime;

namespace Archeus.Battle.Events.Resolvers
{
    public static class DamageCalculationResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt)
        {
            Entity attacker = evt.Source;

            if (!ctx.StatsLookup.HasComponent(attacker)) return;

            float baseDamage = evt.Payload.Damage.BaseDamage;
            float outgoingDamage = baseDamage * evt.Payload.Damage.AttackMultiplier;

            ctx.ChainBuffer.Add(new ChainedBattleEvent
            {
                Event = new BattleEvent
                {
                    Type = BattleEventType.DamageMitigated,
                    Source = attacker,
                    Target = evt.Target,
                    Payload = new EventPayload
                    {
                        Damage = new DamagePayload
                        {
                            AttackMultiplier = evt.Payload.Damage.AttackMultiplier,
                            BaseDamage = baseDamage,
                            FinalDamage = outgoingDamage,
                            Snapshot = evt.Payload.Damage.Snapshot
                        }
                    }
                }
            });
        }
    }
}