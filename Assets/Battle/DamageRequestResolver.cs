using Unity.Entities;
using System;
using DBUS.Battle.Components.Combat;

namespace DBUS.Battle.Resolvers
{
    public static class DamageRequestResolver
    {
        public static void Resolve(ref BattleSimulationContext ctx, BattleEvent evt)
        {
            var attacker = evt.Source;
            var target = evt.Target;

            if (!ctx.StatsLookup.HasComponent(target)) return;

            // 1. CONSUME: Get the BaseDamage that was potentially modified in PreResolution
            float modifiedBase = evt.Payload.Damage.BaseDamage;

            // 2. CALCULATE: Turn Base into Final (Apply Target Stats)
            var targetStats = ctx.StatsLookup[target];
            
            // Example: Simple Defense subtraction (Min damage of 1)
            float finalDamage = Math.Max(1f, modifiedBase - targetStats.Defense);

            // 3. EMIT: The "It actually happened" event
            ctx.ChainBuffer.Add(new ChainedBattleEvent
            {
                Event = new BattleEvent
                {
                    Type = BattleEventType.DamageResolved,
                    Source = attacker,
                    Target = target,
                    Payload = new EventPayload
                    {
                        Damage = new DamagePayload
                        {
                            AttackMultiplier = evt.Payload.Damage.AttackMultiplier,
                            BaseDamage = modifiedBase, // Carry over the modified base
                            FinalDamage = finalDamage  // The actual HP to subtract
                        }
                    }
                }
            });
        }
    }
}