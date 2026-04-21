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
            if (!ctx.HealthLookup.HasComponent(target)) return;

            CurrentHealth hp = ctx.HealthLookup[target];
            float oldHp = hp.Value;
            float damageTaken = evt.Payload.Damage.FinalDamage;

            hp.Value = Math.Max(0f, hp.Value - damageTaken);
            ctx.HealthLookup[target] = hp;

            float maxHealth = evt.Payload.Damage.Snapshot.TargetMaxHealth;
            float hpPercent = hp.Value / maxHealth * 100f;

            Logging.Info(LogCategory.Combat, $"[Health Update] Entity {target.Index}: " + $"{oldHp} -> {hp.Value} (-{damageTaken}) | " +$"Current HP: {hpPercent:F1}%");

        }
    }
}