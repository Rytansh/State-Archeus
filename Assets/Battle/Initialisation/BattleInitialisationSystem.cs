using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.Components.Turns;
using DBUS.Battle.Components.Setup;
using DBUS.Battle.Components.Ownership;
using DBUS.Battle.Components.Combat;
using DBUS.Battle.Components.Events;

[UpdateInGroup(typeof(BattleInitialisationGroup))]
public partial struct BattleInitialisationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (battleState, battle)
         in SystemAPI.Query<RefRO<BattleState>>()
                     .WithAll<BattleTag>()
                     .WithNone<BattleInitialisationCompleteTag>()
                     .WithEntityAccess())
        {
            if (battleState.ValueRO.Phase != BattlePhase.Creating)
                continue;

            ecb.AddComponent(battle, new TurnCounter { CurrentTurn = 0 });

            foreach (var (ownedBattle, player)
                    in SystemAPI.Query<RefRO<OwnedBattle>>()
                                .WithAll<PlayerTag>()
                                .WithEntityAccess())
            {
                if (ownedBattle.ValueRO.Battle != battle)
                    continue;

                ecb.AddComponent(player, new MaxActionPoints { Value = 4 });
                ecb.AddComponent(player, new RemainingActionPoints { Value = 4 });
                ecb.AddComponent(player, new PlayerHand { Current = 0 });
                ecb.AddComponent(player, new MaxHandSize { Value = 4 });

                var triggerBuffer = state.EntityManager.GetBuffer<RegisteredTrigger>(battle);

                triggerBuffer.Add(new RegisteredTrigger
                {
                    EventType = BattleEventType.ActionDeclared,
                    Priority = 10,
                    Owner = player,
                    BehaviourID = 1,
                    RegistrationIndex = triggerBuffer.Length
                });

                triggerBuffer.Add(new RegisteredTrigger
                {
                    EventType = BattleEventType.CardPlaced,
                    Priority = 10,
                    Owner = player,
                    BehaviourID = 2,
                    RegistrationIndex = triggerBuffer.Length
                });
            }

            ecb.AddComponent<BattleInitialisationCompleteTag>(battle);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

