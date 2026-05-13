using Unity.Entities;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Events.Runtime;
using Archeus.Content.Blobs;

namespace Archeus.Battle.Stats
{
    public static class StatResolver
    {
        public static float Resolve(Entity entity, StatType statType, ref BattleContext ctx)
        {
            var stats = ctx.StatsLookup[entity];

            float baseValue = statType switch
            {
                StatType.Attack => stats.Attack,
                StatType.Defense => stats.Defense,
                StatType.MaxHealth => stats.MaxHealth,
                StatType.CritRate => stats.CritRATE,
                StatType.CritDamage => stats.CritDMG,
                _ => 0f
            };

            float flatBonus = 0f;
            float percentBonus = 0f;
            float finalMultiplier = 1f;

            bool hasOverride = false;
            float overrideValue = 0f;

            if (ctx.EffectLookup.HasBuffer(entity))
            {
                DynamicBuffer<ActiveEffect> activeEffects = ctx.EffectLookup[entity];
                ref var registry = ref ctx.BattleRegistryReference.Value;

                for (int i = 0; i < activeEffects.Length; i++)
                {
                    var activeEffect = activeEffects[i];
                    ref EffectBlob effectDef = ref registry.Effects[activeEffect.EffectIndex];

                    for (int j = 0; j < effectDef.StatModifiers.Length; j++)
                    {
                        StatModifier modifier = effectDef.StatModifiers[j];

                        if (modifier.StatType != statType)
                            continue;

                        ApplyModifier(modifier,activeEffect.Strength, ref flatBonus, ref percentBonus, ref finalMultiplier, ref hasOverride, ref overrideValue);
                    }
                }
            }
            
            float finalValue;

            if (!hasOverride)
            {
                finalValue = baseValue;
                finalValue += flatBonus;
                finalValue *= 1f + percentBonus;
                finalValue *= finalMultiplier;
            } else {
                finalValue = overrideValue;
            }

            return finalValue;
        }

        private static void ApplyModifier(StatModifier modifier, float strength, ref float flatBonus, ref float percentBonus, ref float finalMultiplier, ref bool hasOverride, ref float overrideValue)
        {
            switch (modifier.ModifierType)
            {
                case ModifierOperation.AddFlat:
                {
                    flatBonus += strength;
                    break;
                }

                case ModifierOperation.AddPercent:
                {
                    percentBonus += strength / 100f;
                    break;
                }

                case ModifierOperation.MultiplyFinal:
                {
                    finalMultiplier *= strength;
                    break;
                }

                case ModifierOperation.Override:
                {
                    hasOverride = true;
                    overrideValue = strength;
                    break;
                }
            }
        }
    }
}
