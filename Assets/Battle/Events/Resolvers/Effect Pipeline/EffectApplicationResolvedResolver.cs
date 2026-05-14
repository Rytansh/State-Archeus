using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Data.Effects;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Events.Runtime;
using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Battle.Events.Resolvers
{
    public static class EffectApplicationResolvedResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt)
        {
            //validate effect application and apply mutations

            Entity target = evt.Target;

            if (!ctx.EffectLookup.HasBuffer(target))
                return;

            DynamicBuffer<ActiveEffect> activeEffects = ctx.EffectLookup[target];
            ref var registry = ref ctx.BattleRegistryReference.Value;
            ref var effectDef = ref registry.Effects[evt.Payload.Effect.EffectIndex];

            ApplyEffect(activeEffects, evt, effectDef.StackBehaviour);

            Logging.Info(LogCategory.Testing, $"Applied effect {evt.Payload.Effect.EffectIndex} to target {target.Index} with strength {evt.Payload.Effect.Strength}% for {evt.Payload.Effect.Duration} turns. This effect has {effectDef.StatModifiers.Length} stat modifiers.");

            ctx.ChainBuffer.Add(new ChainedBattleEvent
            {
                Event = new BattleEvent
                {
                    Type = BattleEventType.EffectApplied,
                    Scope = BattleEventScope.Targeted,
                    Source = evt.Source,
                    Target = evt.Target,
                    Payload = new EventPayload
                    {
                        Effect = new EffectPayload
                        {
                            EffectIndex = evt.Payload.Effect.EffectIndex,
                            Strength = evt.Payload.Effect.Strength,
                            Duration = evt.Payload.Effect.Duration,
                            IsPermanent = evt.Payload.Effect.IsPermanent
                        }
                    }
                }
            });
        }

        private static void ApplyEffect(DynamicBuffer<ActiveEffect> effects, BattleEvent evt, StackingBehaviour stackBehaviour)
        {
            ActiveEffect effectToAdd = new ActiveEffect
            {
                EffectIndex = evt.Payload.Effect.EffectIndex,
                Strength = evt.Payload.Effect.Strength,
                RemainingDuration = evt.Payload.Effect.Duration,
                IsPermanent = evt.Payload.Effect.IsPermanent,
                Source = evt.Source
            };

            switch(stackBehaviour)
            {
                case StackingBehaviour.AddInstance:
                {
                    effects.Add(effectToAdd);
                    break;
                }
                case StackingBehaviour.RefreshDuration:
                {
                    int existingIndex = FindExistingEffect(effects, effectToAdd.EffectIndex);

                    if (existingIndex == -1)
                    {
                        effects.Add(effectToAdd);
                        break;
                    }

                    ActiveEffect existingEffect = effects[existingIndex];
                    existingEffect.RemainingDuration = effectToAdd.RemainingDuration;
                    effects[existingIndex] = existingEffect;

                    break;
                }
                case StackingBehaviour.IgnoreIfInstanceExists:
                {
                    int existingIndex = FindExistingEffect(effects, effectToAdd.EffectIndex);

                    if (existingIndex == -1)
                    {
                        effects.Add(effectToAdd);
                        break;
                    }

                    break;
                }
                
            }
        }

        private static int FindExistingEffect(DynamicBuffer<ActiveEffect> effects, int effectIndex)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].EffectIndex == effectIndex)
                    return i;
            }

            return -1;
        }
    }
}