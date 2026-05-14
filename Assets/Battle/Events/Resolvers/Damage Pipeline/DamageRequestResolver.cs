using System.Diagnostics;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Events.Runtime;

namespace Archeus.Battle.Events.Resolvers
{
    public static class DamageRequestResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt)
        {
            var attacker = evt.Source;
            var target = evt.Target;

            if (!ctx.StatsLookup.HasComponent(target) || !ctx.StatsLookup.HasComponent(attacker)) return;
            if (!ctx.HealthLookup.HasComponent(target) || !ctx.HealthLookup.HasComponent(attacker)) return;

            ctx.ChainBuffer.Add(new ChainedBattleEvent
            {
                Event = new BattleEvent
                {
                    Type = BattleEventType.DamageConfirmed,
                    Scope = evt.Scope,
                    Source = attacker,
                    Target = target,
                    Payload = new EventPayload
                    {
                        Damage = new DamagePayload
                        {
                            AttackMultiplier = evt.Payload.Damage.AttackMultiplier,
                        }
                    }
                }
            });
        }
    }
}