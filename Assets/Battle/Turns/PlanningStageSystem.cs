using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Turns;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.Components.Requests;
using DBUS.Battle.Components.Ownership;
using DBUS.Battle.Components.Combat;
using DBUS.Battle.Components.Events;

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
        foreach (var (request, requestEntity)
         in SystemAPI.Query<RefRO<PlaceCardRequest>>()
                     .WithEntityAccess())
        {
            var player = request.ValueRO.Player;

            if (!SystemAPI.HasComponent<PlayerHand>(player) || !SystemAPI.HasComponent<RemainingActionPoints>(player) || !TryPlanningBattle(ref state, player, out var battle))
            {
                ecb.DestroyEntity(requestEntity);
                continue;
            }

            var buffer = SystemAPI.GetBuffer<BattleEventBuffer>(battle);

            buffer.Add(new BattleEventBuffer
            {
                Value = new BattleEvent
                {
                    Type = BattleEventType.CardPlaced,
                    Source = player,
                    Target = player
                }
            });

            ecb.DestroyEntity(requestEntity);
        }
    }

    private void ProcessPlayActionRequest(ref SystemState state, EntityCommandBuffer ecb)
    {
        foreach (var (request, requestEntity)
         in SystemAPI.Query<RefRO<PlayActionRequest>>()
                     .WithEntityAccess())
        {
            var player = request.ValueRO.Player;

            if (!SystemAPI.HasComponent<RemainingActionPoints>(player) || !TryPlanningBattle(ref state, player, out var battle))
            {
                ecb.DestroyEntity(requestEntity);
                continue;
            }

            var buffer = SystemAPI.GetBuffer<BattleEventBuffer>(battle);

            buffer.Add(new BattleEventBuffer
            {
                Value = new BattleEvent
                {
                    Type = BattleEventType.ActionDeclared,
                    Source = player,
                    Target = player
                }
            });

            ecb.DestroyEntity(requestEntity);
        }
    }
    private void ProcessEndPlanningRequest(ref SystemState state, EntityCommandBuffer ecb)
    {
        foreach (var (request, requestEntity)
         in SystemAPI.Query<RefRO<EndPlanningRequest>>()
                     .WithEntityAccess())
        {
            var player = request.ValueRO.Player;

            if (!TryPlanningBattle(ref state, player, out var battle) || SystemAPI.HasComponent<BattlePlanningCompleteTag>(battle))
            {
                ecb.DestroyEntity(requestEntity);
                continue;
            }
            Logging.System("[Battle] Planning phase complete.");
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