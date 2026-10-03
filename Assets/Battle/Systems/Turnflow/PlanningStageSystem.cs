using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Factory;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Runtime;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Turnflow
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PlanningStageGroup))]
    public partial struct PlanningStageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            ProcessPlaceCardRequest(ref state, ecb);
            ProcessPlayActionRequest(ref state, ecb);
            ProcessEndPlanningRequest(ref state, ecb);
            //planning phase should not perform actions, only calculate action points, etc, and check if actions can be performed
            //all actions should be added to a queue later

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void ProcessPlaceCardRequest(ref SystemState state, EntityCommandBuffer ecb)
        {
            foreach (
                var (request, requestEntity) in SystemAPI
                    .Query<RefRO<PlaceCardRequest>>()
                    .WithEntityAccess()
            )
            {
                Entity player = request.ValueRO.Player;

                if (
                    !SystemAPI.HasComponent<RemainingActionPoints>(player)
                    || !SystemAPI.HasBuffer<HandCard>(player)
                    || !SystemAPI.HasBuffer<FieldCard>(player)
                    || !TryPlanningBattle(ref state, player, out Entity battle)
                )
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                RefRW<RemainingActionPoints> remainingAP =
                    SystemAPI.GetComponentRW<RemainingActionPoints>(player);

                if (remainingAP.ValueRO.Value <= 0)
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                DynamicBuffer<HandCard> hand = SystemAPI.GetBuffer<HandCard>(player);

                DynamicBuffer<FieldCard> field = SystemAPI.GetBuffer<FieldCard>(player);

                bool deployed = CardZoneResolver.TryDeployCard(
                    request.ValueRO.CardToPlace,
                    request.ValueRO.Position,
                    ref hand,
                    ref field
                );

                if (deployed)
                {
                    remainingAP.ValueRW.Value--;

                    Logging.Info(
                        LogCategory.Combat,
                        $"Card deployed to {request.ValueRO.Position}."
                    );
                }
                else
                {
                    Logging.Warn(LogCategory.Combat, "This slot is occupied!");
                }

                ecb.DestroyEntity(requestEntity);
            }
        }

        private void ProcessPlayActionRequest(ref SystemState state, EntityCommandBuffer ecb)
        {
            foreach (
                var (request, requestEntity) in SystemAPI
                    .Query<RefRO<PlayActionRequest>>()
                    .WithEntityAccess()
            )
            {
                var player = request.ValueRO.Player;
                Logging.Info(LogCategory.Testing, "reached");

                if (
                    !SystemAPI.HasComponent<RemainingActionPoints>(player)
                    || !TryPlanningBattle(ref state, player, out var battle)
                )
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                ecb.DestroyEntity(requestEntity);
            }
        }

        private void ProcessEndPlanningRequest(ref SystemState state, EntityCommandBuffer ecb)
        {
            foreach (
                var (request, requestEntity) in SystemAPI
                    .Query<RefRO<EndPlanningRequest>>()
                    .WithEntityAccess()
            )
            {
                var player = request.ValueRO.Player;

                if (
                    !TryPlanningBattle(ref state, player, out var battle)
                    || SystemAPI.HasComponent<BattlePlanningCompleteTag>(battle)
                )
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }
                Logging.Info(LogCategory.Combat, "Planning phase complete.");
                ecb.AddComponent<BattlePlanningCompleteTag>(battle);
                ecb.DestroyEntity(requestEntity);
            }
        }

        private bool TryPlanningBattle(ref SystemState state, Entity player, out Entity battle)
        {
            battle = Entity.Null;

            if (!SystemAPI.HasComponent<OwnedBattle>(player))
                return false;

            var ownedBattle = SystemAPI.GetComponent<OwnedBattle>(player);
            battle = ownedBattle.Battle;

            if (!SystemAPI.HasComponent<BattleState>(battle))
                return false;

            var battleState = SystemAPI.GetComponent<BattleState>(battle);

            return battleState.Phase == BattlePhase.Planning;
        }
    }
}
