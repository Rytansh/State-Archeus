using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Context;
using Archeus.Battle.Events.Factory;
using Archeus.Battle.Presentation.Factory;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Scheduling;
using Archeus.Battle.Systems.Events;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Actions
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleSimulationGroup))]
    [UpdateAfter(typeof(TurnFlowGroup))]
    [UpdateBefore(typeof(BattleEventProcessingSystem))]
    public partial struct ActionExecutionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<BattleTag>(),
                ComponentType.ReadOnly<ActionSchedulerState>()
            );
            using NativeArray<Entity> battles = query.ToEntityArray(Allocator.Temp);
            foreach (Entity battle in battles)
            {
                if (
                    !ActionQueueService.Healthy(em, battle)
                    || em.GetComponentData<BattleState>(battle).Phase != BattlePhase.Attacking
                    || Archeus.Battle.Systems.Setup.BattlePhaseTransitionSystem.ShouldSkip(
                        em,
                        battle,
                        BattlePhase.Attacking
                    )
                    || !ActionQueueService.TryActive(em, battle, out Entity context, out var data)
                    || !ActionQueueService.IsSettled(em, battle)
                    || ActionControlProcessor.HasPendingChild(em, battle, context)
                    || em.GetBuffer<ActionControlRequest>(battle).Length != 0
                )
                    continue;
                var pending = em.GetBuffer<PlannedAction>(context);
                if (pending.Length == 0)
                    continue;
                var scheduler = em.GetComponentData<ActionSchedulerState>(battle);
                if (++scheduler.DispatchesThisTurn > ActionQueueAlgorithms.MaximumDispatchesPerTurn)
                {
                    ActionQueueService.Fail(em, battle, SchedulerFailure.DispatchLimit);
                    continue;
                }
                var counter = em.GetComponentData<BattleActionExecutionCounter>(battle);
                if (counter.NextID == 0 || counter.NextID == uint.MaxValue)
                {
                    ActionQueueService.Fail(em, battle, SchedulerFailure.IdentityExhausted);
                    continue;
                }
                PlannedAction action = pending[0];
                pending.RemoveAt(0);
                uint executionID = counter.NextID++;
                em.SetComponentData(battle, counter);
                em.SetComponentData(battle, scheduler);
                em.GetBuffer<ActionExecutionState>(battle)
                    .Add(
                        new ActionExecutionState
                        {
                            ActionExecutionID = executionID,
                            QueueActionID = action.QueueActionID,
                            ContextID = data.ContextID,
                            Context = context,
                            Action = action,
                            Outcome = ActionOutcome.AttemptStarted,
                            Source = action.Source,
                            PrimaryTarget = action.PrimaryTarget,
                            ActionType = action.CharacterAction,
                        }
                    );
                ActionOrderPublication.Emit(
                    em,
                    battle,
                    SchedulerFactKind.Dequeued,
                    action,
                    ActionOutcome.AttemptStarted,
                    executionID
                );
                PresentationFactEmitter.EmitActionStartedFact(
                    new PresentationFactContext
                    {
                        BattleRuntimeID = em.GetComponentData<BattleID>(battle).Value,
                        SourceRuntimeID = ActionOrderPublication.RuntimeID(em, action.Source),
                        TargetRuntimeID = ActionOrderPublication.RuntimeID(
                            em,
                            action.PrimaryTarget
                        ),
                        ActionDefinitionID = action.DefinitionID,
                        ActionExecutionID = executionID,
                        ActionResultIndex = PresentationFactMetadata.NoActionResult,
                        HitIndex = PresentationFactMetadata.NoHit,
                    },
                    em.GetBuffer<PresentationFact>(battle),
                    SystemAPI.GetComponentRW<PresentationSequenceCounter>(battle)
                );
                ActionOrderPublication.Emit(
                    em,
                    battle,
                    SchedulerFactKind.AttemptStarted,
                    action,
                    ActionOutcome.AttemptStarted,
                    executionID
                );
                var events = em.GetBuffer<BattleEvent>(battle);
                BattleEventEmitter.EmitOriginEvent(
                    new BattleEvent
                    {
                        Type = BattleEventType.ActionAttemptStarted,
                        Scope = BattleEventScope.Global,
                        Source = action.Source,
                        Target = action.PrimaryTarget,
                        ActionData = new EventActionData
                        {
                            ActionExecutionID = executionID,
                            ActionDefinitionID = action.DefinitionID,
                            QueueActionID = action.QueueActionID,
                            ContextID = data.ContextID,
                            ActionType = action.CharacterAction,
                            CausingExecutionID = action.CausingExecutionID,
                            InheritedSource = action.InheritedSource,
                            ActionResultGroupIndex = EventActionData.NoActionResultGroup,
                            HitIndex = EventActionData.NoHit,
                        },
                    },
                    ref events,
                    SystemAPI.GetComponentRW<BattleEventGroupIDCounter>(battle)
                );
            }
        }
    }
}
