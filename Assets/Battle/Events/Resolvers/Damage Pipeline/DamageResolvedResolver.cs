using System;
using Unity.Entities;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Events.Runtime;
using Archeus.Core.Debugging;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Stats;
using Archeus.Battle.Data.Effects;
using Archeus.Battle.Data.Events;

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

            float maxHealth = StatResolver.Resolve(target, StatType.MaxHealth, ref ctx);
            float hpPercent = hp.Value / maxHealth * 100f;

            bool didCrit = evt.Payload.Damage.DidCrit;
            if (didCrit)
            {
                Logging.Info(LogCategory.Combat, $"[Health Update] CRITICAL HIT!! Entity {target.Index}: " + $"{oldHp} -> {hp.Value} (-{damageToApply}) | " +$"Current HP: {hpPercent:F1}%");
            }
            else
            {
                Logging.Info(LogCategory.Combat, $"[Health Update] Entity {target.Index}: " + $"{oldHp} -> {hp.Value} (-{damageToApply}) | " +$"Current HP: {hpPercent:F1}%");
            }

            ctx.ChainBuffer.Add(new ChainedBattleEvent
            {
                Event = new BattleEvent
                {
                    Type = BattleEventType.DamageApplied,
                    Scope = evt.Scope,
                    Source = evt.Source,
                    Target = target,
                    Payload = new EventPayload
                    {
                        Damage = new DamagePayload
                        {
                            AttackMultiplier = evt.Payload.Damage.AttackMultiplier,
                            BaseDamage = evt.Payload.Damage.BaseDamage,
                            FinalDamage = evt.Payload.Damage.FinalDamage,
                            DidCrit = evt.Payload.Damage.DidCrit,
                            CritMultiplier = evt.Payload.Damage.CritMultiplier
                        }
                    }
                }
            });
        }
    }
}