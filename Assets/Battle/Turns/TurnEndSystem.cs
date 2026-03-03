using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Turns;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.Components.Requests;
using DBUS.Battle.Components.Ownership;

[UpdateInGroup(typeof(TurnEndGroup))]
public partial struct TurnEndSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (battleState, battle)
                 in SystemAPI.Query<
                     RefRO<BattleState>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleTurnEndCompleteTag>()
                    .WithEntityAccess())
        {
            if (battleState.ValueRO.Phase != BattlePhase.TurnEnd)
                continue;

            ecb.AddComponent<BattleTurnEndCompleteTag>(battle);
            Logging.System("[Battle] Ending turn.");
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}


