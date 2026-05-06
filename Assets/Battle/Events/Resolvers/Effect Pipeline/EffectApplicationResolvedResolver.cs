using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Events.Definitions;
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

            var target = evt.Target;

            if (!ctx.EffectLookup.HasBuffer(target))
                return;

            DynamicBuffer<ActiveEffect> activeEffects = ctx.EffectLookup[target];

            activeEffects.Add(new ActiveEffect
            {
                EffectIndex = evt.Payload.Effect.EffectIndex,
                Strength = evt.Payload.Effect.Strength,
                RemainingDuration = evt.Payload.Effect.Duration
            });

            Logging.Info(LogCategory.Testing, $"Applied effect {evt.Payload.Effect.EffectIndex} to target {target.Index} with strength {evt.Payload.Effect.Strength}% for {evt.Payload.Effect.Duration} turns.");

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
                            Duration = evt.Payload.Effect.Duration
                        }
                    }
                }
            });
        }
    }
}