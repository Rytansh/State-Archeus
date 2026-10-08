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

            ProcessEndPlanningRequests(ref state, ecb);
            //planning phase should not perform actions, only calculate action points, etc, and check if actions can be performed
            //all actions should be added to a queue later

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void ProcessEndPlanningRequests(ref SystemState state, EntityCommandBuffer ecb)
        {
            foreach (
                var (request, requestEntity) in SystemAPI
                    .Query<RefRO<EndDrawingRequest>>()
                    .WithEntityAccess()
            )
            {
                Entity player = request.ValueRO.Player;

                if (
                    !TurnCommandValidator.TryGetActiveBattle(
                        ref state,
                        player,
                        BattlePhase.Drawing,
                        out Entity battle
                    ) || SystemAPI.HasComponent<BattlePlanningCompleteTag>(battle)
                )
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                ecb.AddComponent<BattlePlanningCompleteTag>(battle);

                Logging.Info(
                    LogCategory.Combat,
                    $"Player {player.Index} completed the Planning Stage."
                );

                ecb.DestroyEntity(requestEntity);
            }
        }
    }
}
