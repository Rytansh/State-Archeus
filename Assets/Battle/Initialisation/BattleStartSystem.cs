using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.Components.Turns;
using DBUS.Battle.Components.Setup;
using DBUS.Battle.Components.Ownership;
using DBUS.Battle.Components.Events;
public partial struct BattleStartSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (battleState, battle)
                in SystemAPI.Query<RefRO<BattleState>>()
                            .WithAll<BattleTag>()
                            .WithNone<BattleTurnStartTag>()
                            .WithEntityAccess())
        {
            if (battleState.ValueRO.Phase != BattlePhase.BattleReady)
                continue;
            
            var buffer = state.EntityManager.GetBuffer<BattleEventBuffer>(battle);
            buffer.Add(new BattleEventBuffer
            {
                Value = new BattleEvent
                {
                    Type = BattleEventType.TurnStarted,
                    Source = battle,
                    Target = battle
                }
            });

            var triggerBuffer = SystemAPI.GetBuffer<RegisteredTrigger>(battle);

            ecb.AddComponent<BattleTurnStartTag>(battle);

            Logging.System("[Battle] Battle starting.");
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
        
}