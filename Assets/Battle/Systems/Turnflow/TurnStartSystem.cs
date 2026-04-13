using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Events.Payloads;
using Archeus.Core.Debugging;

namespace Archeus.Battle.Systems.Turnflow
{
    [UpdateInGroup(typeof(TurnStartGroup))]
    public partial struct TurnStartSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (battleState, turnCounter, battle) in SystemAPI.Query<RefRO<BattleState>,RefRW<TurnCounter>>().WithAll<BattleTag>().WithNone<BattleTurnStartCompleteTag>().WithEntityAccess())
            {
                if (battleState.ValueRO.Phase != BattlePhase.TurnStart)
                    continue;

                turnCounter.ValueRW.CurrentTurn++;
                Logging.Info(LogCategory.Combat, $" Starting turn {turnCounter.ValueRW.CurrentTurn}.");
                var eventBuffer = SystemAPI.GetBuffer<BattleEvent>(battle);

                eventBuffer.Add(new BattleEvent
                    {
                        Type = BattleEventType.TurnStarted,
                        Scope = BattleEventScope.Global,
                        Source = battle,
                        Target = Entity.Null,
                        Payload = new EventPayload{}
                    });

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
                    remainingAP.ValueRW.Value = maxAP.ValueRO.Value;
                }

                ecb.AddComponent<BattleTurnStartCompleteTag>(battle);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

