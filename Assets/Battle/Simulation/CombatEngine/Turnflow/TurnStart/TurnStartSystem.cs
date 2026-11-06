using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Factory;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Scheduling;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Turnflow
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(TurnStartGroup))]
    public partial struct TurnStartSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (
                var (battleState, turnCounter, activePlayer, battle) in SystemAPI
                    .Query<RefRO<BattleState>, RefRW<TurnCounter>, RefRO<ActiveTurnPlayer>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleTurnStartCompleteTag>()
                    .WithEntityAccess()
            )
            {
                if (battleState.ValueRO.Phase != BattlePhase.TurnStart)
                    continue;
                if (
                    Archeus.Battle.Systems.Setup.BattlePhaseTransitionSystem.ShouldSkip(
                        state.EntityManager,
                        battle,
                        BattlePhase.TurnStart
                    )
                )
                    continue;
                if (
                    !ActionQueueService.TryActive(
                        state.EntityManager,
                        battle,
                        out _,
                        out var context
                    )
                    || context.ExecutionType != AttackingStageExecutionType.NormalTurn
                )
                    continue;

                Entity player = activePlayer.ValueRO.Player;
                if (player == Entity.Null || !state.EntityManager.Exists(player))
                {
                    Logging.Error(
                        LogCategory.Combat,
                        $"Battle {battle.Index} entered TurnStart without a valid active player."
                    );

                    continue;
                }

                turnCounter.ValueRW.CurrentTurn++;
                Logging.Info(
                    LogCategory.Combat,
                    $" Starting turn {turnCounter.ValueRW.CurrentTurn}."
                );

                RefreshAllRetreatRestrictions(ref state, ecb, battle);

                EmitTurnStartedEvent(ref state, battle);

                RefreshPlayerAP(ref state, player);

                ecb.AddComponent<BattleTurnStartCompleteTag>(battle);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void RefreshAllRetreatRestrictions(
            ref SystemState state,
            EntityCommandBuffer ecb,
            Entity battle
        )
        {
            foreach (
                var (ownedBattle, card) in SystemAPI
                    .Query<RefRO<OwnedBattle>>()
                    .WithAll<RetreatedThisTurn>()
                    .WithEntityAccess()
            )
            {
                if (ownedBattle.ValueRO.Battle != battle)
                    continue;

                ecb.RemoveComponent<RetreatedThisTurn>(card);
            }
        }

        private void RefreshPlayerAP(ref SystemState state, Entity player)
        {
            int maxAP = SystemAPI.GetComponent<MaxActionPoints>(player).Value;

            SystemAPI.GetComponentRW<RemainingActionPoints>(player).ValueRW.Value = maxAP;
        }

        private void EmitTurnStartedEvent(ref SystemState state, Entity battle)
        {
            DynamicBuffer<BattleEvent> eventBuffer = SystemAPI.GetBuffer<BattleEvent>(battle);
            RefRW<BattleEventGroupIDCounter> groupCounter =
                SystemAPI.GetComponentRW<BattleEventGroupIDCounter>(battle);

            BattleEventEmitter.EmitOriginEvent(
                new BattleEvent
                {
                    Type = BattleEventType.TurnStarted,
                    Scope = BattleEventScope.Global,
                    Source = battle,
                    Target = Entity.Null,
                    Payload = new EventPayload { },
                },
                ref eventBuffer,
                groupCounter
            );
        }
    }
}
