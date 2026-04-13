using System;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Events.Runtime;
using Archeus.Core.Debugging;

namespace Archeus.Battle.Events.Resolvers
{
    public static class DamageResolvedResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt)
        {
            var target = evt.Target;
            if (!ctx.HealthLookup.HasComponent(target)) return;

            var hp = ctx.HealthLookup[target];
            float oldHp = hp.Value;
            float damageTaken = evt.Payload.Damage.FinalDamage;

            // Apply the damage
            hp.Value = Math.Max(0f, hp.Value - damageTaken);
            ctx.HealthLookup[target] = hp;

            // Get MaxHP for percentage tracking (assuming you have a MaxHP component or stat)
            // If MaxHP is in stats:
            var stats = ctx.StatsLookup[target];
            float hpPercent = hp.Value / stats.MaxHealth * 100f;

            Logging.Info(LogCategory.Combat, $"[Health Update] Entity {target.Index}: " + $"{oldHp} -> {hp.Value} (-{damageTaken}) | " +$"Current HP: {hpPercent:F1}%");

        }
    }
}