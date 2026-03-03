using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Turns;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.Components.Requests;
using DBUS.Battle.Components.Ownership;
using DBUS.Battle.Components.Combat;

[UpdateInGroup(typeof(DrawingStageGroup))]
public partial struct DrawingStageSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (battleState, battle)
                 in SystemAPI.Query<
                     RefRO<BattleState>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleDrawingCompleteTag>()
                    .WithEntityAccess())
        {
            if (battleState.ValueRO.Phase != BattlePhase.Drawing)
                continue;

            foreach (var (ownedBattle, hand, max, player)
                    in SystemAPI.Query<
                                RefRO<OwnedBattle>,
                                RefRW<PlayerHand>,
                                RefRO<MaxHandSize>>()
                                .WithAll<PlayerTag>()
                                .WithEntityAccess())
            {
                if (ownedBattle.ValueRO.Battle != battle)
                    continue;
                int cardsBeforeFilled = hand.ValueRW.Current;
                hand.ValueRW.Current = max.ValueRO.Value;
                Logging.System("[Battle] Player's hand replenished from " + cardsBeforeFilled + " to " + hand.ValueRW.Current + ". Drawing phase complete.");
            }
            ecb.AddComponent<BattleDrawingCompleteTag>(battle);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
