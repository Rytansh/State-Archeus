using System;
using System.Collections.Generic;
using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Presentation.State;
using Unity.Entities;

namespace Archeus.Battle.Scheduling
{
    /// <summary>Non-structural scheduler mutations; context creation is deferred to the phase controller.</summary>
    public static class ActionControlProcessor
    {
        public static bool Enqueue(
            EntityManager em,
            Entity battle,
            ActionControlRequest request,
            out ulong id
        )
        {
            id = 0;
            if (
                !ActionQueueService.Healthy(em, battle)
                || !em.HasBuffer<ActionControlRequest>(battle)
            )
                return false;
            var pending = em.GetBuffer<ActionControlRequest>(battle);
            if (pending.Length >= ActionQueueAlgorithms.MaximumControlsPerBoundary)
            {
                ActionQueueService.Fail(em, battle, SchedulerFailure.OperationLimit);
                return false;
            }
            var counter = em.GetComponentData<ActionSchedulerCounters>(battle);
            if (request.RequestID == 0)
            {
                if (counter.NextControlID == 0 || counter.NextControlID == ulong.MaxValue)
                {
                    ActionQueueService.Fail(em, battle, SchedulerFailure.IdentityExhausted);
                    return false;
                }
                request.RequestID = counter.NextControlID++;
                em.SetComponentData(battle, counter);
            }
            else if (request.RequestID >= counter.NextControlID)
                return false;
            id = request.RequestID;
            pending.Add(request);
            return true;
        }

        public static void ApplyFrame(EntityManager em, Entity battle, uint frameID)
        {
            if (
                !ActionQueueService.Healthy(em, battle)
                || !em.HasBuffer<ActionControlRequest>(battle)
            )
                return;
            var requests = em.GetBuffer<ActionControlRequest>(battle);
            var eligible = new List<ActionControlRequest>();
            for (int i = requests.Length - 1; i >= 0; i--)
                if (requests[i].FrameID == frameID)
                {
                    eligible.Add(requests[i]);
                    requests.RemoveAt(i);
                }
            eligible.Sort(
                (a, b) =>
                    ActionQueueAlgorithms.CompareControl(
                        a.Priority,
                        a.OwnerRuntimeID,
                        a.RegistrationIndex,
                        a.TriggerIndex,
                        a.RequestID,
                        b.Priority,
                        b.OwnerRuntimeID,
                        b.RegistrationIndex,
                        b.TriggerIndex,
                        b.RequestID
                    )
            );
            int nextPosition = 0;
            foreach (var original in eligible)
            {
                var request = original;
                bool front =
                    request.Kind == ActionControlKind.InsertNext
                    || request.Kind == ActionControlKind.ReplaceCurrent;
                if (front)
                    request.Position = nextPosition;
                var state = em.GetComponentData<ActionSchedulerState>(battle);
                var results = em.GetBuffer<ActionControlResult>(battle);
                bool duplicate = request.RequestID <= state.RetiredControlID;
                foreach (var previous in results)
                    if (previous.RequestID == request.RequestID)
                        duplicate = true;
                if (duplicate)
                    continue;
                if (++state.ControlsThisTurn > 2048)
                {
                    em.SetComponentData(battle, state);
                    ActionQueueService.Fail(em, battle, SchedulerFailure.OperationLimit);
                    return;
                }
                em.SetComponentData(battle, state);
                bool accepted = Apply(em, battle, request);
                if (accepted && front)
                    nextPosition++;
                // No structural changes occur in Apply, so this buffer remains valid.
                results.Add(
                    new ActionControlResult { RequestID = request.RequestID, Accepted = accepted }
                );
            }
        }

        private static bool Apply(EntityManager em, Entity battle, ActionControlRequest request)
        {
            if (
                !ActionQueueService.Healthy(em, battle)
                || !ActionQueueService.TryActive(em, battle, out Entity context, out var data)
                || data.ContextID != request.ContextID
            )
                return false;
            BattlePhase phase = em.GetComponentData<BattleState>(battle).Phase;
            if (
                phase != BattlePhase.Attacking
                && phase != BattlePhase.Planning
                && request.Kind != ActionControlKind.SkipTurn
                && request.Kind != ActionControlKind.SkipStages
            )
                return false;
            var pending = em.GetBuffer<PlannedAction>(context);
            var storage = new EcsActionQueue(pending);
            switch (request.Kind)
            {
                case ActionControlKind.InterruptCurrent:
                case ActionControlKind.CancelCurrent:
                case ActionControlKind.ReplaceCurrent:
                {
                    var states = em.GetBuffer<ActionExecutionState>(battle);
                    int index = -1;
                    for (int i = 0; i < states.Length; i++)
                        if (
                            states[i].ActionExecutionID == request.ExecutionID
                            && states[i].ContextID == data.ContextID
                        )
                            index = i;
                    if (index < 0 || !ActionQueueAlgorithms.CanInterrupt(states[index].Outcome))
                        return false;
                    var current = states[index];
                    PlannedAction replacement = request.Action;
                    uint inserted = 0;
                    if (request.Kind == ActionControlKind.ReplaceCurrent)
                    {
                        replacement.Origin = ActionOrigin.InterruptReplacement;
                        replacement.CausingExecutionID = current.ActionExecutionID;
                        replacement.InheritedSource = current.Source;
                        replacement.ReservedAP = 0;
                        if (
                            !ActionQueueService.Validate(
                                em,
                                battle,
                                data,
                                ref replacement,
                                false,
                                out _
                            )
                            || !ActionQueueService.TryInsert(
                                em,
                                battle,
                                context,
                                replacement,
                                Math.Min(request.Position, pending.Length),
                                SchedulerFactKind.Inserted,
                                out inserted,
                                false
                            )
                        )
                            return false;
                    }
                    current.Outcome =
                        request.Kind == ActionControlKind.CancelCurrent
                            ? ActionOutcome.Cancelled
                            : ActionOutcome.Interrupted;
                    states[index] = current;
                    ActionOrderPublication.Emit(
                        em,
                        battle,
                        current.Outcome == ActionOutcome.Interrupted
                            ? SchedulerFactKind.Interrupted
                            : SchedulerFactKind.Cancelled,
                        current.Action,
                        current.Outcome,
                        current.ActionExecutionID
                    );
                    if (inserted != 0)
                    {
                        replacement.QueueActionID = inserted;
                        replacement.ContextID = data.ContextID;
                        ActionOrderPublication.Emit(
                            em,
                            battle,
                            SchedulerFactKind.Inserted,
                            replacement
                        );
                    }
                    return true;
                }
                case ActionControlKind.RemovePending:
                    return ActionQueueService.TryRemove(
                        em,
                        battle,
                        context,
                        request.TargetQueueActionID,
                        false
                    );
                case ActionControlKind.MovePending:
                    if (
                        !ActionQueueAlgorithms.Move<PlannedAction, EcsActionQueue>(
                            ref storage,
                            request.TargetQueueActionID,
                            request.Position
                        )
                    )
                        return false;
                    ActionOrderPublication.Emit(
                        em,
                        battle,
                        SchedulerFactKind.Moved,
                        pending[request.Position]
                    );
                    return true;
                case ActionControlKind.Append:
                case ActionControlKind.InsertNext:
                case ActionControlKind.InsertAt:
                case ActionControlKind.InsertAfter:
                case ActionControlKind.ReplacePending:
                {
                    PlannedAction action = request.Action;
                    action.ReservedAP = 0;
                    if (action.Origin == ActionOrigin.PlayerPlanned)
                        action.Origin = ActionOrigin.Passive;
                    action.CausingExecutionID = request.ExecutionID;
                    if (!ActionQueueService.Validate(em, battle, data, ref action, false, out _))
                        return false;
                    int position =
                        request.Kind == ActionControlKind.Append
                            ? pending.Length
                            : request.Position;
                    if (request.Kind == ActionControlKind.InsertNext)
                        position = Math.Min(request.Position, pending.Length);
                    if (request.Kind == ActionControlKind.InsertAfter)
                    {
                        position = ActionQueueAlgorithms.Find<PlannedAction, EcsActionQueue>(
                            ref storage,
                            request.RelativeQueueActionID
                        );
                        if (position < 0)
                            return false;
                        position++;
                    }
                    if (request.Kind == ActionControlKind.ReplacePending)
                    {
                        position = ActionQueueAlgorithms.Find<PlannedAction, EcsActionQueue>(
                            ref storage,
                            request.TargetQueueActionID
                        );
                        var counter = em.GetComponentData<ActionSchedulerCounters>(battle);
                        if (
                            position < 0
                            || counter.NextQueueActionID == 0
                            || counter.NextQueueActionID == uint.MaxValue
                        )
                            return false;
                        ActionQueueService.TryRemove(
                            em,
                            battle,
                            context,
                            request.TargetQueueActionID,
                            false
                        );
                    }
                    return ActionQueueService.TryInsert(
                        em,
                        battle,
                        context,
                        action,
                        position,
                        SchedulerFactKind.Inserted,
                        out _
                    );
                }
                case ActionControlKind.ExtraAction:
                case ActionControlKind.ExtraTurn:
                {
                    Entity actor = request.Action.Source;
                    if (
                        phase != BattlePhase.Attacking
                        || !ActionQueueService.IsLivingFieldActor(em, actor, battle)
                    )
                        return false;
                    if (
                        request.Kind == ActionControlKind.ExtraAction
                        && !ActionQueueService.CanReceiveExtraAction(em, actor, battle)
                    )
                        return false;
                    if (data.Depth >= ActionQueueAlgorithms.MaximumContextDepth)
                    {
                        ActionQueueService.Fail(em, battle, SchedulerFailure.ContextDepth);
                        return false;
                    }
                    var transitions = em.GetBuffer<PendingContextTransition>(battle);
                    if (transitions.Length >= ActionQueueAlgorithms.MaximumControlsPerBoundary)
                    {
                        ActionQueueService.Fail(em, battle, SchedulerFailure.OperationLimit);
                        return false;
                    }
                    transitions.Add(
                        new PendingContextTransition
                        {
                            RequestID = request.RequestID,
                            Parent = context,
                            Player = em.GetComponentData<CardOwner>(actor).Player,
                            Character = actor,
                            Type =
                                request.Kind == ActionControlKind.ExtraAction
                                    ? AttackingStageExecutionType.ExtraAction
                                    : AttackingStageExecutionType.ExtraTurn,
                        }
                    );
                    return true;
                }
                case ActionControlKind.SkipTurn:
                case ActionControlKind.SkipStages:
                {
                    if (
                        !ActionQueueService.Belongs(em, request.TargetPlayer, battle)
                        || !em.HasComponent<PlayerTag>(request.TargetPlayer)
                    )
                        return false;
                    BattleStageMask mask =
                        request.Kind == ActionControlKind.SkipTurn
                            ? BattleStageMask.EntireTurn
                            : request.Stages;
                    if (mask == BattleStageMask.None || (mask & ~BattleStageMask.EntireTurn) != 0)
                        return false;
                    uint scope =
                        request.Kind == ActionControlKind.SkipStages
                        && request.TargetPlayer == data.ActingPlayer
                            ? data.ContextID
                            : 0;
                    em.GetBuffer<PendingStageSkip>(battle)
                        .Add(
                            new PendingStageSkip
                            {
                                Player = request.TargetPlayer,
                                ContextID = scope,
                                Stages = mask,
                            }
                        );
                    return true;
                }
                case ActionControlKind.OfferInterruptSelection:
                {
                    if (
                        phase != BattlePhase.Planning
                        || pending.Length == 0
                        || !ActionQueueService.Belongs(em, request.TargetPlayer, battle)
                        || request.TargetPlayer == data.ActingPlayer
                        || !em.HasComponent<PlayerTag>(request.TargetPlayer)
                    )
                        return false;
                    var counter = em.GetComponentData<ActionSchedulerCounters>(battle);
                    if (counter.NextSelectionID == 0 || counter.NextSelectionID == uint.MaxValue)
                        return false;
                    var offers = em.GetBuffer<PendingInterruptSelection>(battle);
                    if (offers.Length >= ActionQueueAlgorithms.MaximumControlsPerBoundary)
                        return false;
                    offers.Add(
                        new PendingInterruptSelection
                        {
                            SelectionID = counter.NextSelectionID++,
                            ContextID = data.ContextID,
                            Player = request.TargetPlayer,
                            PhaseEpoch = em.GetComponentData<BattlePhaseEpoch>(battle).Value,
                        }
                    );
                    em.SetComponentData(battle, counter);
                    return true;
                }
            }
            return false;
        }

        public static bool SelectInterrupt(
            EntityManager em,
            Entity battle,
            Entity player,
            uint selectionID,
            uint queueID
        )
        {
            if (
                !ActionQueueService.Healthy(em, battle)
                || !ActionQueueService.TryActive(em, battle, out Entity context, out var data)
                || em.GetComponentData<BattleState>(battle).Phase != BattlePhase.Planning
            )
                return false;
            var offers = em.GetBuffer<PendingInterruptSelection>(battle);
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                if (
                    offer.SelectionID != selectionID
                    || offer.Player != player
                    || offer.ContextID != data.ContextID
                    || offer.PhaseEpoch != em.GetComponentData<BattlePhaseEpoch>(battle).Value
                )
                    continue;
                if (!ActionQueueService.TryRemove(em, battle, context, queueID, false))
                    return false;
                offers.RemoveAt(i);
                return true;
            }
            return false;
        }

        public static bool HasPendingChild(EntityManager em, Entity battle, Entity context)
        {
            if (!em.HasBuffer<PendingContextTransition>(battle))
                return false;
            foreach (var transition in em.GetBuffer<PendingContextTransition>(battle))
                if (transition.Parent == context)
                    return true;
            return false;
        }
    }
}
