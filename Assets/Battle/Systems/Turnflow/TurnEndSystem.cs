using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Buffers.Events;
using Archeus.Core.Debugging;

namespace Archeus.Battle.Systems.Turnflow
{
    [UpdateInGroup(typeof(TurnEndGroup))]
    public partial struct TurnEndSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (battleState, battle) in SystemAPI.Query<RefRO<BattleState>>().WithAll<BattleTag>().WithNone<BattleTurnEndCompleteTag>().WithEntityAccess())
            {
                if (battleState.ValueRO.Phase != BattlePhase.TurnEnd)
                    continue;
                
                DynamicBuffer<BattleEvent> eventBuffer = SystemAPI.GetBuffer<BattleEvent>(battle);

                eventBuffer.Add(new BattleEvent
                    {
                        Type = BattleEventType.TurnEnded,
                        Scope = BattleEventScope.Global,
                        Source = battle,
                        Target = Entity.Null,
                        Payload = new EventPayload{}
                    });

                ecb.AddComponent<BattleTurnEndCompleteTag>(battle);
                Logging.Info(LogCategory.Combat, "Ending turn.");
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


