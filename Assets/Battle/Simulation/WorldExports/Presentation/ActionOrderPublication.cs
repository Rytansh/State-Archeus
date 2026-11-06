using System;
using System.Collections.Generic;
using System.Text;
using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.State;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Scheduling
{
    public static class ActionOrderPublication
    {
        public static uint RuntimeID(EntityManager em, Entity entity) =>
            em.Exists(entity) && em.HasComponent<CardRuntimeID>(entity)
                ? em.GetComponentData<CardRuntimeID>(entity).Value
                : 0;

        public static void Emit(
            EntityManager em,
            Entity battle,
            SchedulerFactKind kind,
            PlannedAction action,
            ActionOutcome outcome = ActionOutcome.Queued,
            uint executionID = 0
        )
        {
            var payload = new SchedulerFactPayload
            {
                Kind = kind,
                QueueActionID = action.QueueActionID,
                ContextID = action.ContextID,
                ExecutionID = executionID,
                CausingExecutionID = action.CausingExecutionID,
                SourceRuntimeID = RuntimeID(em, action.Source),
                TargetRuntimeID = RuntimeID(em, action.PrimaryTarget),
                InheritedSourceRuntimeID = RuntimeID(em, action.InheritedSource),
                ActionType = action.CharacterAction,
                Origin = action.Origin,
                Outcome = outcome,
            };
            EmitPayload(em, battle, payload);
        }

        public static void EmitContext(
            EntityManager em,
            Entity battle,
            SchedulerFactKind kind,
            AttackingStageExecutionContext context
        )
        {
            uint parentID =
                em.Exists(context.ParentContext)
                && em.HasComponent<AttackingStageExecutionContext>(context.ParentContext)
                    ? em.GetComponentData<AttackingStageExecutionContext>(
                        context.ParentContext
                    ).ContextID
                    : 0;
            EmitPayload(
                em,
                battle,
                new SchedulerFactPayload
                {
                    Kind = kind,
                    ContextID = context.ContextID,
                    ParentContextID = parentID,
                    ContextType = context.ExecutionType,
                }
            );
        }

        private static void EmitPayload(
            EntityManager em,
            Entity battle,
            SchedulerFactPayload payload
        )
        {
            if (
                !em.HasBuffer<PresentationFact>(battle)
                || !em.HasComponent<PresentationSequenceCounter>(battle)
                || !em.HasComponent<BattleID>(battle)
            )
                return;
            var counter = em.GetComponentData<PresentationSequenceCounter>(battle);
            if (counter.NextSequence == uint.MaxValue)
                return;
            em.GetBuffer<PresentationFact>(battle)
                .Add(
                    new PresentationFact
                    {
                        FactType = PresentationFactType.SchedulerTransition,
                        FactMetadata = new PresentationFactMetadata
                        {
                            BattleRuntimeID = em.GetComponentData<BattleID>(battle).Value,
                            Sequence = counter.NextSequence++,
                            ActionExecutionID = payload.ExecutionID,
                            SourceRuntimeID = payload.SourceRuntimeID,
                            TargetRuntimeID = payload.TargetRuntimeID,
                        },
                        FactPayload = new PresentationFactPayload { Scheduler = payload },
                    }
                );
            em.SetComponentData(battle, counter);
        }

        public static ActionOrderSnapshot Capture(EntityManager em, Entity battle)
        {
            var result = new ActionOrderSnapshot
            {
                BattleID = em.GetComponentData<BattleID>(battle).Value,
            };
            if (em.HasComponent<BattlePhaseEpoch>(battle))
                result.PhaseEpoch = em.GetComponentData<BattlePhaseEpoch>(battle).Value;
            if (em.HasComponent<ActionSchedulerState>(battle))
                result.Failure = em.GetComponentData<ActionSchedulerState>(battle).Failure;
            Entity active = Entity.Null;
            if (ActionQueueService.TryActive(em, battle, out active, out var current))
                result.ActiveContextID = current.ContextID;
            var contexts = new List<ActionOrderContextView>();
            using (
                var query = em.CreateEntityQuery(
                    ComponentType.ReadOnly<AttackingStageExecutionContext>(),
                    ComponentType.ReadOnly<OwnedBattle>()
                )
            )
            using (NativeArray<Entity> all = query.ToEntityArray(Allocator.Temp))
                foreach (Entity entity in all)
                {
                    var data = em.GetComponentData<AttackingStageExecutionContext>(entity);
                    if (
                        em.GetComponentData<OwnedBattle>(entity).Battle != battle
                        || data.Battle != battle
                        || data.ExecutionStatus == AttackingStageExecutionStatus.Completed
                    )
                        continue;
                    var row = new ActionOrderContextView
                    {
                        ContextID = data.ContextID,
                        Type = data.ExecutionType,
                        Status = data.ExecutionStatus,
                        RestrictedActor = RuntimeID(em, data.ActingCharacter),
                        RemainingAP =
                            entity == active
                            && em.Exists(data.ActingPlayer)
                            && em.HasComponent<RemainingActionPoints>(data.ActingPlayer)
                                ? em.GetComponentData<RemainingActionPoints>(
                                    data.ActingPlayer
                                ).Value
                                : data.RemainingActionPoints,
                        PlayerID = PlayerID(em, data.ActingPlayer),
                        ParentID = em.Exists(data.ParentContext)
                            ? em.GetComponentData<AttackingStageExecutionContext>(
                                data.ParentContext
                            ).ContextID
                            : 0,
                    };
                    var queue = em.GetBuffer<PlannedAction>(entity);
                    row.Pending = new ActionOrderItem[queue.Length];
                    for (int i = 0; i < queue.Length; i++)
                        row.Pending[i] = Item(em, queue[i], 0, ActionOutcome.Queued);
                    contexts.Add(row);
                }
            contexts.Sort((a, b) => a.ContextID.CompareTo(b.ContextID));
            result.Contexts = contexts.ToArray();
            if (em.HasBuffer<ActionExecutionState>(battle))
                foreach (var state in em.GetBuffer<ActionExecutionState>(battle))
                {
                    result.HasExecuting = true;
                    result.Executing = Item(
                        em,
                        state.Action,
                        state.ActionExecutionID,
                        state.Outcome
                    );
                    break;
                }
            if (active != Entity.Null)
            {
                Entity player = em.GetComponentData<AttackingStageExecutionContext>(
                    active
                ).ActingPlayer;
                result.Actors = FieldIDs(em, player);
                var targets = new List<uint>();
                if (em.HasBuffer<BattlePlayerReference>(battle))
                    foreach (var reference in em.GetBuffer<BattlePlayerReference>(battle))
                        if (reference.Player != player)
                            targets.AddRange(FieldIDs(em, reference.Player));
                targets.Sort();
                result.Targets = targets.ToArray();
            }
            if (em.HasBuffer<PendingInterruptSelection>(battle))
            {
                var selections = em.GetBuffer<PendingInterruptSelection>(battle);
                result.Selections = new InterruptSelectionView[selections.Length];
                for (int i = 0; i < selections.Length; i++)
                    result.Selections[i] = new InterruptSelectionView
                    {
                        SelectionID = selections[i].SelectionID,
                        ContextID = selections[i].ContextID,
                        PlayerID = PlayerID(em, selections[i].Player),
                        Side = em.GetComponentData<BattleSideAssignment>(selections[i].Player).Side,
                    };
            }
            return result;
        }

        private static ulong PlayerID(EntityManager em, Entity player) =>
            em.Exists(player) && em.HasComponent<BattlePlayerIdentity>(player)
                ? em.GetComponentData<BattlePlayerIdentity>(player).PlayerID
                : 0;

        private static uint[] FieldIDs(EntityManager em, Entity player)
        {
            var ids = new List<uint>();
            if (em.Exists(player) && em.HasBuffer<FieldCard>(player))
                foreach (var card in em.GetBuffer<FieldCard>(player))
                {
                    uint id = RuntimeID(em, card.Card);
                    if (id != 0)
                        ids.Add(id);
                }
            ids.Sort();
            return ids.ToArray();
        }

        private static ActionOrderItem Item(
            EntityManager em,
            PlannedAction action,
            uint executionID,
            ActionOutcome outcome
        ) =>
            new ActionOrderItem
            {
                QueueActionID = action.QueueActionID,
                ExecutionID = executionID,
                ContextID = action.ContextID,
                SourceRuntimeID = RuntimeID(em, action.Source),
                TargetRuntimeID = RuntimeID(em, action.PrimaryTarget),
                CausingExecutionID = action.CausingExecutionID,
                InheritedSourceRuntimeID = RuntimeID(em, action.InheritedSource),
                DefinitionID = action.DefinitionID,
                ActionType = action.CharacterAction,
                Origin = action.Origin,
                Outcome = outcome,
                TargetMode = action.TargetMode,
            };

        public static string Fingerprint(ActionOrderSnapshot state)
        {
            var text = new StringBuilder()
                .Append(state.ActiveContextID)
                .Append(':')
                .Append(state.PhaseEpoch)
                .Append(':')
                .Append(state.Failure);
            foreach (var context in state.Contexts)
            {
                text.Append('|')
                    .Append(context.ContextID)
                    .Append(':')
                    .Append(context.ParentID)
                    .Append(':')
                    .Append(context.PlayerID)
                    .Append(':')
                    .Append(context.RestrictedActor)
                    .Append(':')
                    .Append(context.Type)
                    .Append(':')
                    .Append(context.Status)
                    .Append(':')
                    .Append(context.RemainingAP);
                foreach (var action in context.Pending)
                    Append(text, action);
            }
            if (state.HasExecuting)
                Append(text, state.Executing);
            foreach (uint actor in state.Actors)
                text.Append('a').Append(actor);
            foreach (uint target in state.Targets)
                text.Append('t').Append(target);
            foreach (var selection in state.Selections)
                text.Append('s')
                    .Append(selection.SelectionID)
                    .Append(':')
                    .Append(selection.ContextID)
                    .Append(':')
                    .Append(selection.PlayerID);
            return text.ToString();
        }

        private static void Append(StringBuilder text, ActionOrderItem item) =>
            text.Append('/')
                .Append(item.QueueActionID)
                .Append(':')
                .Append(item.ExecutionID)
                .Append(':')
                .Append(item.SourceRuntimeID)
                .Append(':')
                .Append(item.TargetRuntimeID)
                .Append(':')
                .Append(item.ContextID)
                .Append(':')
                .Append(item.CausingExecutionID)
                .Append(':')
                .Append(item.InheritedSourceRuntimeID)
                .Append(':')
                .Append(item.DefinitionID)
                .Append(':')
                .Append(item.TargetMode)
                .Append(':')
                .Append(item.ActionType)
                .Append(':')
                .Append(item.Origin)
                .Append(':')
                .Append(item.Outcome);
    }
}
