using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Factory;
using Archeus.Battle.Events.Payloads;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Turnflow
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(TurnEndGroup))]
    public partial struct TurnEndSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (battleState, battle) in SystemAPI
                    .Query<RefRO<BattleState>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleTurnEndCompleteTag>()
                    .WithEntityAccess()
            )
            {
                if (battleState.ValueRO.Phase != BattlePhase.TurnEnd)
                    continue;

                DynamicBuffer<BattleEvent> eventBuffer = SystemAPI.GetBuffer<BattleEvent>(battle);
                RefRW<BattleEventGroupIDCounter> groupCounter =
                    SystemAPI.GetComponentRW<BattleEventGroupIDCounter>(battle);

                BattleEventEmitter.EmitOriginEvent(
                    new BattleEvent
                    {
                        Type = BattleEventType.TurnEnded,
                        Scope = BattleEventScope.Global,
                        Source = battle,
                        Target = Entity.Null,
                        Payload = new EventPayload { },
                    },
                    ref eventBuffer,
                    groupCounter
                );

                ecb.AddComponent<BattleTurnEndCompleteTag>(battle);
                ecb.AddComponent<EffectDurationsProcessingTag>(battle);
                Logging.Info(LogCategory.Combat, "Ending turn.");
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
