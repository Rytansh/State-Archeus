using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Context;
using Archeus.Battle.Events.Factory;
using Archeus.Battle.Presentation.Factory;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Systems.Events;
using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Battle.Systems.Actions
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleSimulationGroup))]
    [UpdateBefore(typeof(BattleEventProcessingSystem))]
    public partial struct ActionExecutionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (
                var (
                    requests,
                    actionStates,
                    eventQueue,
                    factQueue,
                    actionCounter,
                    groupCounter,
                    presentationSequenceCounter,
                    battle
                ) in SystemAPI
                    .Query<
                        DynamicBuffer<ActionExecutionRequest>,
                        DynamicBuffer<ActionExecutionState>,
                        DynamicBuffer<BattleEvent>,
                        DynamicBuffer<PresentationFact>,
                        RefRW<BattleActionExecutionCounter>,
                        RefRW<BattleEventGroupIDCounter>,
                        RefRW<PresentationSequenceCounter>
                    >()
                    .WithAll<BattleTag>()
                    .WithEntityAccess()
            )
            {
                if (requests.Length == 0)
                    continue;

                if (eventQueue.Length > 0)
                    continue;

                ActionExecutionRequest request = requests[0];

                requests.RemoveAt(0);

                if (!TryGetActionEventType(request.CharacterAction, out BattleEventType eventType))
                {
                    Logging.Warn(
                        LogCategory.Combat,
                        $"Unsupported action type: {request.CharacterAction}"
                    );

                    continue;
                }

                uint executionID = actionCounter.ValueRO.NextID;

                actionCounter.ValueRW.NextID++;

                actionStates.Add(
                    new ActionExecutionState
                    {
                        ActionExecutionID = executionID,
                        NextResultGroupIndex = 0,

                        Source = request.Source,
                        PrimaryTarget = request.PrimaryTarget,
                        ActionType = request.CharacterAction,
                    }
                );

                BattleEvent actionEvent = new BattleEvent
                {
                    Type = eventType,
                    Scope = BattleEventScope.Targeted,

                    Source = request.Source,
                    Target = request.PrimaryTarget,

                    ActionData = new EventActionData
                    {
                        ActionExecutionID = executionID,

                        ActionType = request.CharacterAction,

                        ActionResultGroupIndex = EventActionData.NoActionResultGroup,
                    },
                };

                ulong battleRuntimeID = SystemAPI.GetComponent<BattleID>(battle).Value;

                uint sourceRuntimeID = SystemAPI.GetComponent<CardRuntimeID>(request.Source).Value;

                uint targetRuntimeID = SystemAPI
                    .GetComponent<CardRuntimeID>(request.PrimaryTarget)
                    .Value;
                PresentationFactContext presentationContext = new PresentationFactContext
                {
                    BattleRuntimeID = battleRuntimeID,

                    SourceRuntimeID = sourceRuntimeID,
                    TargetRuntimeID = targetRuntimeID,

                    ActionDefinitionID = 0,
                    ActionExecutionID = executionID,
                    ActionResultIndex = PresentationFactMetadata.NoActionResult,

                    GroupID = EventStructuralData.InvalidGroupID,
                    Generation = 0,
                };

                PresentationFactEmitter.EmitActionStartedFact(
                    presentationContext,
                    factQueue,
                    presentationSequenceCounter
                );

                DynamicBuffer<BattleEvent> writableEventQueue = eventQueue;

                BattleEventEmitter.EmitOriginEvent(
                    actionEvent,
                    ref writableEventQueue,
                    groupCounter
                );

                Logging.Info(
                    LogCategory.Simulation,
                    $"Action Started:"
                        + $"Execution={executionID} | "
                        + $"Type={request.CharacterAction} | "
                        + $"Source={request.Source.Index}"
                );
            }
        }

        private static bool TryGetActionEventType(
            CharacterActionType actionType,
            out BattleEventType eventType
        )
        {
            switch (actionType)
            {
                case CharacterActionType.NormalAttack:
                    eventType = BattleEventType.TestEvent;
                    return true;

                // add more later

                default:
                    eventType = default;
                    return false;
            }
        }
    }
}
