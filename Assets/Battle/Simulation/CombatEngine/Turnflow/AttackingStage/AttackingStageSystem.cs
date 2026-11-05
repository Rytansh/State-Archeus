using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
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

                // ActionExecutionSystem consumes the committed queue over successive updates.
                // Do not leave Attacking while actions or their causal effects are outstanding.
                if (
                    (
                        SystemAPI.HasBuffer<ActionExecutionRequest>(battle)
                        && SystemAPI.GetBuffer<ActionExecutionRequest>(battle).Length != 0
                    )
                    || (
                        SystemAPI.HasBuffer<ActionExecutionState>(battle)
                        && SystemAPI.GetBuffer<ActionExecutionState>(battle).Length != 0
                    )
                    || (
                        SystemAPI.HasBuffer<BattleEvent>(battle)
                        && SystemAPI.GetBuffer<BattleEvent>(battle).Length != 0
                    )
                    || (
                        SystemAPI.HasBuffer<ChainedBattleEvent>(battle)
                        && SystemAPI.GetBuffer<ChainedBattleEvent>(battle).Length != 0
                    )
                    || (
                        SystemAPI.HasBuffer<BehaviourExecutionRequest>(battle)
                        && SystemAPI.GetBuffer<BehaviourExecutionRequest>(battle).Length != 0
                    )
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
