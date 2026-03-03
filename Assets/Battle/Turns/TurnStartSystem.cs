using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Turns;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.Components.Requests;
using DBUS.Battle.Components.Ownership;

[UpdateInGroup(typeof(TurnStartGroup))]
public partial struct TurnStartSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (battleState, turnCounter, battle)
                 in SystemAPI.Query<
                     RefRO<BattleState>,
                     RefRW<TurnCounter>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleTurnStartCompleteTag>()
                    .WithEntityAccess())
        {
            if (battleState.ValueRO.Phase != BattlePhase.TurnStart)
                continue;

            turnCounter.ValueRW.CurrentTurn++;
            Logging.System($"[Turn] Starting turn {turnCounter.ValueRW.CurrentTurn}.");

            foreach (var (ownedBattle, remainingAP, maxAP, player)
                    in SystemAPI.Query<
                                RefRO<OwnedBattle>,
                                RefRW<RemainingActionPoints>,
                                RefRO<MaxActionPoints>>()
                                .WithAll<PlayerTag>()
                                .WithEntityAccess())
            {
                if (ownedBattle.ValueRO.Battle != battle)
                    continue;
                int apBeforeFilled = remainingAP.ValueRW.Value;
                remainingAP.ValueRW.Value = maxAP.ValueRO.Value;
                Logging.System("[Battle] Player's AP replenished from " + apBeforeFilled + " to " + remainingAP.ValueRW.Value + ".");
            }

            ecb.AddComponent<BattleTurnStartCompleteTag>(battle);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

