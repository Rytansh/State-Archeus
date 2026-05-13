using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Systems.Turnflow;
using Archeus.Core.Debugging;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Components.Core;

namespace Archeus.Battle.Systems.Effects
{
    [UpdateInGroup(typeof(TurnEndGroup))]
    [UpdateAfter(typeof(TurnEndSystem))]
    public partial struct EffectDurationProcessingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, battle) in SystemAPI.Query<RefRO<BattleState>>().WithAll<BattleTag, EffectDurationsProcessingTag>().WithEntityAccess())
            {
                foreach (var (ownedBattle, effectsBuffer, entity) in SystemAPI.Query<RefRO<OwnedBattle>, DynamicBuffer<ActiveEffect>>().WithEntityAccess())
                {
                    if (ownedBattle.ValueRO.Battle != battle)
                        continue;

                    DynamicBuffer<BattleEvent> battleEvents = SystemAPI.GetBuffer<BattleEvent>(battle);
                    DynamicBuffer<ActiveEffect> activeEffects = effectsBuffer;

                    for (int i = activeEffects.Length - 1; i >= 0; i--)
                    {
                        ActiveEffect effect = activeEffects[i];

                        if (effect.IsPermanent)
                        {
                            Logging.Info(LogCategory.Testing,$"Ticked effect {effect.EffectIndex} on entity {entity.Index}. Remaining: Permanent");
                            continue;
                        }

                        effect.RemainingDuration--;

                        if (effect.RemainingDuration <= 0)
                        {
                            Logging.Info(LogCategory.Testing, $"Effect {effect.EffectIndex} expired on entity {entity.Index}.");

                            battleEvents.Add(new BattleEvent
                            {
                                Type = BattleEventType.EffectExpired,
                                Scope = BattleEventScope.Targeted,
                                Source = battle,
                                Target = entity,
                                Payload = new EventPayload
                                {
                                    Effect = new EffectPayload
                                    {
                                        EffectIndex = effect.EffectIndex,
                                        Strength = effect.Strength,
                                        Duration = 0
                                    }
                                }
                            });

                            activeEffects.RemoveAt(i);
                            continue;
                        }
                        activeEffects[i] = effect;

                        Logging.Info(LogCategory.Testing,$"Ticked effect {effect.EffectIndex} on entity {entity.Index}. Remaining: {effect.RemainingDuration}");
                    }
                }
                ecb.RemoveComponent<EffectDurationsProcessingTag>(battle);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

