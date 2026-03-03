using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.Components.Setup;
using DBUS.Battle.Components.Turns;
using DBUS.Battle.Components.Ownership;

[UpdateInGroup(typeof(BattleRootGroup))]
[UpdateAfter(typeof(BattleSimulationGroup))]
public partial struct BattlePhaseTransitionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (battleState, battle)
                 in SystemAPI.Query<RefRW<BattleState>>()
                             .WithAll<BattleTag>()
                             .WithEntityAccess())
        {
            switch (battleState.ValueRO.Phase)
            {
                case BattlePhase.Creating:
                    if (SystemAPI.HasComponent<BattleInitialisationCompleteTag>(battle))
                    {
                        ecb.RemoveComponent<BattleInitialisationCompleteTag>(battle);
                        battleState.ValueRW.Phase = BattlePhase.Initialising;
                    }
                    break;

                case BattlePhase.Initialising:
                    if (SystemAPI.HasComponent<BattleSpawnRequestsIssuedTag>(battle))
                    {
                        ecb.RemoveComponent<BattleSpawnRequestsIssuedTag>(battle);
                        battleState.ValueRW.Phase = BattlePhase.Spawning;
                    }
                    break;

                case BattlePhase.Spawning:
                    if (SystemAPI.HasComponent<BattleSpawningCompleteTag>(battle))
                    {
                        ecb.RemoveComponent<BattleSpawningCompleteTag>(battle);
                        battleState.ValueRW.Phase = BattlePhase.BattleReady;
                    }
                    break;

                case BattlePhase.BattleReady:
                    if (SystemAPI.HasComponent<BattleTurnStartTag>(battle))
                    {
                        ecb.RemoveComponent<BattleTurnStartTag>(battle);
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
