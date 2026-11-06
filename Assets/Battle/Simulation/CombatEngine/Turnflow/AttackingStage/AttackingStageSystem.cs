using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Scheduling;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Turnflow
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(AttackingStageGroup))]
    public partial struct AttackingStageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (battleState, battle) in SystemAPI
                    .Query<RefRO<BattleState>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleAttackingCompleteTag>()
                    .WithEntityAccess()
            )
            {
                if (battleState.ValueRO.Phase != BattlePhase.Attacking)
                    continue;

                if (
                    !ActionQueueService.Healthy(state.EntityManager, battle)
                    || !ActionQueueService.TryActive(
                        state.EntityManager,
                        battle,
                        out Entity context,
                        out _
                    )
                    || SystemAPI.GetBuffer<PlannedAction>(context).Length != 0
                    || !ActionQueueService.IsSettled(state.EntityManager, battle)
                    || SystemAPI.GetBuffer<ActionControlRequest>(battle).Length != 0
                    || ActionControlProcessor.HasPendingChild(state.EntityManager, battle, context)
                )
                    continue;

                ecb.AddComponent<BattleAttackingCompleteTag>(battle);
                Logging.Info(LogCategory.Combat, "Attacking stage complete.");
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
