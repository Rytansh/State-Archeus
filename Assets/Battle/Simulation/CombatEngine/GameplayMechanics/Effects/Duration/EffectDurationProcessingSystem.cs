using System;
using System.Collections.Generic;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Scheduling;
using Archeus.Battle.Systems.Turnflow;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Effects
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(TurnEndGroup))]
    [UpdateAfter(typeof(TurnEndSystem))]
    public partial struct EffectDurationProcessingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (_, battle) in SystemAPI
                    .Query<RefRO<BattleState>>()
                    .WithAll<BattleTag, EffectDurationsProcessingTag>()
                    .WithEntityAccess()
            )
            {
                if (
                    state.EntityManager.HasComponent<ActionSchedulerState>(battle)
                    && (
                        !ActionQueueService.TryActive(
                            state.EntityManager,
                            battle,
                            out _,
                            out var context
                        )
                        || context.ExecutionType != AttackingStageExecutionType.NormalTurn
                    )
                )
                {
                    ecb.RemoveComponent<EffectDurationsProcessingTag>(battle);
                    continue;
                }
                using var holders = state.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<OwnedBattle>(),
                    ComponentType.ReadWrite<ActiveEffect>()
                );
                using NativeArray<Entity> allHolders = holders.ToEntityArray(Allocator.Temp);
                EntityManager entities = state.EntityManager;
                var battleHolders = new List<Entity>();
                foreach (Entity holder in allHolders)
                    if (entities.GetComponentData<OwnedBattle>(holder).Battle == battle)
                    {
                        if (!entities.HasComponent<CardRuntimeID>(holder))
                            throw new InvalidOperationException(
                                "Effect holders require a stable CardRuntimeID for deterministic ordering."
                            );
                        battleHolders.Add(holder);
                    }
                Entity[] ordered = battleHolders.ToArray();
                // Expiration emits gameplay events. Chunk order is not stable after recreation,
                // so use the same persistent card identity used for passive trigger ordering.
                Array.Sort(
                    ordered,
                    (a, b) =>
                        entities
                            .GetComponentData<CardRuntimeID>(a)
                            .Value.CompareTo(entities.GetComponentData<CardRuntimeID>(b).Value)
                );
                foreach (Entity entity in ordered)
                {
                    if (entities.GetComponentData<OwnedBattle>(entity).Battle != battle)
                        continue;

                    DynamicBuffer<BattleEvent> battleEvents = SystemAPI.GetBuffer<BattleEvent>(
                        battle
                    );
                    DynamicBuffer<ActiveEffect> activeEffects = SystemAPI.GetBuffer<ActiveEffect>(
                        entity
                    );

                    for (int i = activeEffects.Length - 1; i >= 0; i--)
                    {
                        ActiveEffect effect = activeEffects[i];

                        if (effect.IsPermanent)
                        {
                            Logging.Info(
                                LogCategory.Testing,
                                $"Effect {effect.EffectIndex} on entity {entity.Index} is permanent."
                            );
                            continue;
                        }

                        effect.RemainingDuration--;

                        if (effect.RemainingDuration <= 0)
                        {
                            Logging.Info(
                                LogCategory.Testing,
                                $"Effect {effect.EffectIndex} expired on entity {entity.Index}."
                            );

                            battleEvents.Add(
                                new BattleEvent
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
                                            Duration = 0,
                                        },
                                    },
                                }
                            );

                            activeEffects.RemoveAt(i);
                            continue;
                        }
                        activeEffects[i] = effect;

                        Logging.Info(
                            LogCategory.Testing,
                            $"Ticked effect {effect.EffectIndex} on entity {entity.Index}. Remaining: {effect.RemainingDuration}"
                        );
                    }
                }
                ecb.RemoveComponent<EffectDurationsProcessingTag>(battle);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
