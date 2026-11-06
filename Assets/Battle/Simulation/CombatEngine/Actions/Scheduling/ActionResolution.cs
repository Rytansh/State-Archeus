using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Context;
using Archeus.Battle.Events.Factory;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Runtime;
using Unity.Entities;

namespace Archeus.Battle.Scheduling
{
    public static class ActionResolution
    {
        public static int Find(DynamicBuffer<ActionExecutionState> states, uint executionID)
        {
            for (int i = 0; i < states.Length; i++)
                if (states[i].ActionExecutionID == executionID)
                    return i;
            return -1;
        }

        public static bool Suppress(ref BattleContext ctx, BattleEvent evt)
        {
            if (
                evt.Type != BattleEventType.ActionAttemptStarted
                && evt.Type != BattleEventType.ActionPerformed
            )
                return false;
            int index = Find(ctx.ActionExecutionStates, evt.ActionData.ActionExecutionID);
            if (index < 0)
                return true;
            var outcome = ctx.ActionExecutionStates[index].Outcome;
            return outcome == ActionOutcome.Interrupted
                || outcome == ActionOutcome.Cancelled
                || outcome == ActionOutcome.Failed;
        }

        public static void Authorize(
            BattleEvent evt,
            ref BattleContext ctx,
            in EventEmissionContext emission
        )
        {
            int index = Find(ctx.ActionExecutionStates, evt.ActionData.ActionExecutionID);
            if (index < 0)
                return;
            var state = ctx.ActionExecutionStates[index];
            if (state.Outcome != ActionOutcome.AttemptStarted)
                return;
            var em = ctx.Entities;
            PlannedAction action = state.Action;
            if (
                !ActionQueueService.TryActive(em, ctx.Battle, out Entity context, out var data)
                || context != state.Context
                || !ActionQueueService.Validate(
                    em,
                    ctx.Battle,
                    data,
                    ref action,
                    action.Origin == ActionOrigin.PlayerPlanned,
                    out var definition
                )
            )
            {
                state.Outcome = ActionOutcome.Failed;
                ctx.ActionExecutionStates[index] = state;
                return;
            }
            if (definition.EnergyCost > 0)
            {
                var energy = em.GetComponentData<HeldSuperchargedEnergy>(action.Source);
                energy.Value -= definition.EnergyCost;
                em.SetComponentData(action.Source, energy);
            }
            state.Action = action;
            state.Source = action.Source;
            state.PrimaryTarget = action.PrimaryTarget;
            state.ActionType = action.CharacterAction;
            state.Outcome = ActionOutcome.Authorized;
            ctx.ActionExecutionStates[index] = state;
            if (action.TargetMode == ActionTargetMode.SingleEnemy)
                em.GetBuffer<ActionTargetHistory>(context)
                    .Add(
                        new ActionTargetHistory
                        {
                            Source = action.Source,
                            Target = action.PrimaryTarget,
                        }
                    );
            ActionOrderPublication.Emit(
                em,
                ctx.Battle,
                SchedulerFactKind.Authorized,
                action,
                state.Outcome,
                state.ActionExecutionID
            );
            BattleEventEmitter.EmitContinuationEvent(
                new BattleEvent
                {
                    Type = BattleEventType.ActionPerformed,
                    Scope = BattleEventScope.Global,
                    Source = action.Source,
                    Target = action.PrimaryTarget,
                },
                ref ctx.ChainedEventQueue,
                in emission
            );
        }

        public static void Perform(
            BattleEvent evt,
            ref BattleContext ctx,
            in EventEmissionContext emission
        )
        {
            int index = Find(ctx.ActionExecutionStates, evt.ActionData.ActionExecutionID);
            if (index < 0)
                return;
            var state = ctx.ActionExecutionStates[index];
            if (state.ProgramQueued || state.Outcome != ActionOutcome.Authorized)
                return;
            if (!ActionQueueService.Definition(ctx.Entities, state.Action, out var definition))
            {
                state.Outcome = ActionOutcome.Failed;
                ctx.ActionExecutionStates[index] = state;
                return;
            }
            state.ProgramQueued = true;
            ctx.ActionExecutionStates[index] = state;
            if (definition.ProgramIndex >= 0)
            {
                ctx.ExecutionRequests.Add(
                    new BehaviourExecutionRequest
                    {
                        IsActionProgram = true,
                        ProgramIndex = definition.ProgramIndex,
                        RegistrationIndex = -1,
                        Owner = state.Source,
                        OwnerRuntimeID = ActionOrderPublication.RuntimeID(
                            ctx.Entities,
                            state.Source
                        ),
                        Source = state.Source,
                        Target = state.PrimaryTarget,
                        EmissionContext = emission,
                    }
                );
                return;
            }
            var operation = emission;
            operation.ActionData.ActionResultGroupIndex = ActionResultGroupAllocator.Allocate(
                state.ActionExecutionID,
                ctx.ActionExecutionStates
            );
            operation.ActionData.HitIndex = ActionHitIndexAllocator.Allocate(
                state.ActionExecutionID,
                ctx.ActionExecutionStates
            );
            operation.ExecutionData.OperationID = OperationIDAllocator.Allocate(
                ctx.OperationCounter
            );
            float multiplier =
                state.ActionType
                == Archeus.Battle.Data.Actions.CharacterActionType.SuperchargedAttack
                    ? 2f
                    : 1f;
            EmitDamage(state.PrimaryTarget, state.Source, multiplier, ref ctx, in operation);
        }

        private static void EmitDamage(
            Entity target,
            Entity source,
            float multiplier,
            ref BattleContext ctx,
            in EventEmissionContext emission
        )
        {
            if (target == Entity.Null)
                return;
            BattleEventEmitter.EmitContinuationEvent(
                new BattleEvent
                {
                    Type = BattleEventType.DamageRequested,
                    Scope = BattleEventScope.Targeted,
                    Source = source,
                    Target = target,
                    Payload = new EventPayload
                    {
                        Damage = new DamagePayload { AttackMultiplier = multiplier },
                    },
                },
                ref ctx.ChainedEventQueue,
                in emission
            );
        }

        public static void MarkApplied(ref BattleContext ctx, uint executionID)
        {
            int index = Find(ctx.ActionExecutionStates, executionID);
            if (index < 0)
                return;
            var state = ctx.ActionExecutionStates[index];
            if (state.Outcome == ActionOutcome.Authorized)
            {
                state.Outcome = ActionOutcome.ConsequencesApplied;
                ctx.ActionExecutionStates[index] = state;
            }
        }

        public static void Finish(ref BattleContext ctx, ActionExecutionState state)
        {
            ActionOutcome outcome = state.Outcome;
            if (outcome == ActionOutcome.Authorized || outcome == ActionOutcome.ConsequencesApplied)
                outcome = ActionOutcome.Completed;
            else if (outcome == ActionOutcome.AttemptStarted)
                outcome = ActionOutcome.Failed;
            var em = ctx.Entities;
            if (em.HasBuffer<ActionOutcomeRecord>(ctx.Battle))
            {
                var history = em.GetBuffer<ActionOutcomeRecord>(ctx.Battle);
                foreach (var previous in history)
                    if (previous.ExecutionID == state.ActionExecutionID)
                        return;
                if (history.Length >= 256)
                    history.RemoveAt(0);
                history.Add(
                    new ActionOutcomeRecord
                    {
                        QueueActionID = state.QueueActionID,
                        ExecutionID = state.ActionExecutionID,
                        ContextID = state.ContextID,
                        Outcome = outcome,
                        CausingExecutionID = state.Action.CausingExecutionID,
                    }
                );
            }
            var type =
                outcome == ActionOutcome.Completed ? PresentationFactType.ActionCompleted
                : outcome == ActionOutcome.Interrupted ? PresentationFactType.ActionInterrupted
                : outcome == ActionOutcome.Cancelled ? PresentationFactType.ActionCancelled
                : PresentationFactType.ActionFailed;
            uint sequence = ctx.PresentationSequenceCounter.ValueRO.NextSequence;
            ctx.PresentationSequenceCounter.ValueRW.NextSequence++;
            ctx.PresentationFactQueue.Add(
                new Archeus.Battle.Buffers.Presentation.PresentationFact
                {
                    FactType = type,
                    FactMetadata = new PresentationFactMetadata
                    {
                        BattleRuntimeID = ctx.BattleID,
                        Sequence = sequence,
                        ActionExecutionID = state.ActionExecutionID,
                        ActionDefinitionID = state.Action.DefinitionID,
                        SourceRuntimeID = ActionOrderPublication.RuntimeID(em, state.Source),
                        TargetRuntimeID = ActionOrderPublication.RuntimeID(em, state.PrimaryTarget),
                        HitIndex = PresentationFactMetadata.NoHit,
                        ActionResultIndex = PresentationFactMetadata.NoActionResult,
                    },
                }
            );
            ActionOrderPublication.Emit(
                em,
                ctx.Battle,
                outcome == ActionOutcome.Completed ? SchedulerFactKind.Completed
                    : outcome == ActionOutcome.Interrupted || outcome == ActionOutcome.Cancelled
                        ? SchedulerFactKind.AttemptSettled
                    : SchedulerFactKind.Failed,
                state.Action,
                outcome,
                state.ActionExecutionID
            );
        }
    }
}
