using System;
using Unity.Entities;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Events.Runtime;
using Archeus.Core.Debugging;
using Archeus.Battle.Components.Stats;

namespace Archeus.Battle.Events.Resolvers
{
    public static class DamageResolvedResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt)
        {
            Entity target = evt.Target;

            CurrentHealth hp = ctx.HealthLookup[target];
            float oldHp = hp.Value;

            float damageToApply = evt.Payload.Damage.FinalDamage;

            // Apply the damage
            hp.Value = Math.Max(0f, hp.Value - damageToApply);
            ctx.HealthLookup[target] = hp;

            // Get MaxHP for percentage tracking (assuming you have a MaxHP component or stat)
            // If MaxHP is in stats:
            CharacterStats stats = ctx.StatsLookup[target];
            float hpPercent = hp.Value / stats.MaxHealth * 100f;

            Logging.Info(LogCategory.Combat, $"[Health Update] Entity {target.Index}: " + $"{oldHp} -> {hp.Value} (-{damageToApply}) | " +$"Current HP: {hpPercent:F1}%");

        }
    }
}