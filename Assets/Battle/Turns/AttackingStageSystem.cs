using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Turns;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.Components.Requests;
using DBUS.Battle.Components.Ownership;
using DBUS.Battle.Components.Combat;

[UpdateInGroup(typeof(AttackingStageGroup))]
public partial struct AttackingStageSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (battleState, battle)
                 in SystemAPI.Query<
                     RefRO<BattleState>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleAttackingCompleteTag>()
                    .WithEntityAccess())
        {
            if (battleState.ValueRO.Phase != BattlePhase.Attacking)
                continue;

            //perform all actions in planning queue here
            ecb.AddComponent<BattleAttackingCompleteTag>(battle);
            Logging.System("[Battle] Attacking stage complete.");
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
