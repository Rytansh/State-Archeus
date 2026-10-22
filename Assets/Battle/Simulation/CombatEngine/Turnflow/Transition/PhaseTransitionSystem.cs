using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Setup
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleRootGroup))]
    [UpdateAfter(typeof(BattleSimulationGroup))]
    public partial struct BattlePhaseTransitionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (battleState, battle) in SystemAPI
                    .Query<RefRW<BattleState>>()
                    .WithAll<BattleTag>()
                    .WithEntityAccess()
            )
            {
                switch (battleState.ValueRO.Phase)
                {
                    case BattlePhase.Setup:
                        if (SystemAPI.HasComponent<BattleReadyTag>(battle))
                        {
                            ecb.RemoveComponent<BattleReadyTag>(battle);
                            battleState.ValueRW.Phase = BattlePhase.TurnStart;
                        }
                        break;
                    case BattlePhase.TurnStart:
                        if (SystemAPI.HasComponent<BattleTurnStartCompleteTag>(battle))
                        {
                            ecb.RemoveComponent<BattleTurnStartCompleteTag>(battle);
                            battleState.ValueRW.Phase = BattlePhase.Drawing;
                        }
                        break;
                    case BattlePhase.Drawing:
                        if (SystemAPI.HasComponent<BattleDrawingCompleteTag>(battle))
                        {
                            ecb.RemoveComponent<BattleDrawingCompleteTag>(battle);
                            battleState.ValueRW.Phase = BattlePhase.Planning;
                        }
                        break;
                    case BattlePhase.Planning:
                        if (SystemAPI.HasComponent<BattlePlanningCompleteTag>(battle))
                        {
                            ecb.RemoveComponent<BattlePlanningCompleteTag>(battle);
                            battleState.ValueRW.Phase = BattlePhase.Attacking;
                        }
                        break;
                    case BattlePhase.Attacking:
                        if (SystemAPI.HasComponent<BattleAttackingCompleteTag>(battle))
                        {
                            ecb.RemoveComponent<BattleAttackingCompleteTag>(battle);
                            battleState.ValueRW.Phase = BattlePhase.TurnEnd;
                        }
                        break;
                    case BattlePhase.TurnEnd:
                        if (SystemAPI.HasComponent<BattleTurnEndCompleteTag>(battle))
                        {
                            ecb.RemoveComponent<BattleTurnEndCompleteTag>(battle);
                            battleState.ValueRW.Phase = BattlePhase.TurnStart;
                        }
                        break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
