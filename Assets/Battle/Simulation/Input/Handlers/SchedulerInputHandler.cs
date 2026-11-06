using Archeus.Battle.Buffers.Input;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Scheduling;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Input
{
    /// <summary>Manual queue edits and authorized selectors; automatic controls require explicit development authority.</summary>
    public static class SchedulerInputHandler
    {
        public static bool Handle(
            EntityManager em,
            BattleInputCommand command,
            BattleInputContext input
        )
        {
            if (!ActionQueueService.TryActive(em, input.Battle, out Entity context, out var data))
                return false;
            Entity source = Resolve(em, input.Battle, command.CardRuntimeID);
            Entity target = Resolve(em, input.Battle, command.TargetRuntimeID);
            if (command.Type == BattleInputCommandType.SelectInterrupt)
                return ActionControlProcessor.SelectInterrupt(
                    em,
                    input.Battle,
                    input.Player,
                    command.SelectionID,
                    command.QueueActionID
                );
            if (command.Type == BattleInputCommandType.DebugSchedulerControl)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (
                    !em.HasComponent<SchedulerDebugAuthority>(input.Player)
                    || !em.HasComponent<CardOwner>(source)
                    || em.GetComponentData<CardOwner>(source).Player != input.Player
                )
                    return false;
                Entity targetPlayer =
                    em.Exists(target) && em.HasComponent<CardOwner>(target)
                        ? em.GetComponentData<CardOwner>(target).Player
                        : Entity.Null;
                var state = em.GetBuffer<Archeus.Battle.Buffers.Actions.ActionExecutionState>(
                    input.Battle
                );
                var request = new ActionControlRequest
                {
                    ContextID = data.ContextID,
                    ExecutionID = state.Length == 0 ? 0 : state[0].ActionExecutionID,
                    Kind = command.ControlKind,
                    OwnerRuntimeID = command.CardRuntimeID,
                    TargetPlayer = targetPlayer,
                    Stages = (BattleStageMask)command.Position,
                    Position = command.Position,
                    TargetQueueActionID = command.QueueActionID,
                    RelativeQueueActionID = command.QueueActionID,
                    Action = new PlannedAction
                    {
                        ActionType = PlannedActionType.CharacterAction,
                        Source = source,
                        PrimaryTarget = target,
                        CharacterAction = command.ActionType,
                        Origin = ActionOrigin.Passive,
                    },
                };
                bool arm =
                    command.ControlKind == ActionControlKind.InterruptCurrent
                    || command.ControlKind == ActionControlKind.CancelCurrent
                    || command.ControlKind == ActionControlKind.ReplaceCurrent
                    || command.ControlKind == ActionControlKind.ExtraAction
                    || command.ControlKind == ActionControlKind.ExtraTurn;
                if (arm && state.Length == 0)
                {
                    var armed = em.GetBuffer<ArmedActionControl>(input.Battle);
                    if (armed.Length >= ActionQueueAlgorithms.MaximumControlsPerBoundary)
                        return false;
                    armed.Add(new ArmedActionControl { Request = request });
                    return true;
                }
                return ActionControlProcessor.Enqueue(em, input.Battle, request, out _);
#else
                return false;
#endif
            }
            if (
                data.ActingPlayer != input.Player
                || em.GetComponentData<BattleState>(input.Battle).Phase != BattlePhase.Planning
                || em.HasComponent<BattlePlanningCompleteTag>(input.Battle)
            )
                return false;
            if (command.Type == BattleInputCommandType.QueueAction)
                return ActionQueueService.TryPlan(
                    em,
                    input.Battle,
                    input.Player,
                    new PlannedAction
                    {
                        ActionType = PlannedActionType.CharacterAction,
                        Source = source,
                        PrimaryTarget = target,
                        CharacterAction = command.ActionType,
                        DefinitionID = command.DefinitionID,
                    },
                    out _
                );
            if (
                command.Type == BattleInputCommandType.RemoveAction
                || command.Type == BattleInputCommandType.MoveAction
            )
            {
                var editable = new EcsActionQueue(em.GetBuffer<PlannedAction>(context));
                int index = ActionQueueAlgorithms.Find<PlannedAction, EcsActionQueue>(
                    ref editable,
                    command.QueueActionID
                );
                if (
                    index < 0
                    || editable[index].Origin != ActionOrigin.PlayerPlanned
                    || editable[index].SubmittedByPlayer != input.Player
                )
                    return false;
            }
            if (command.Type == BattleInputCommandType.RemoveAction)
                return ActionQueueService.TryRemove(
                    em,
                    input.Battle,
                    context,
                    command.QueueActionID,
                    true
                );
            if (command.Type == BattleInputCommandType.MoveAction)
            {
                var buffer = em.GetBuffer<PlannedAction>(context);
                var queue = new EcsActionQueue(buffer);
                if (
                    !ActionQueueAlgorithms.Move<PlannedAction, EcsActionQueue>(
                        ref queue,
                        command.QueueActionID,
                        command.Position
                    )
                )
                    return false;
                ActionOrderPublication.Emit(
                    em,
                    input.Battle,
                    SchedulerFactKind.Moved,
                    buffer[command.Position]
                );
                return true;
            }
            return false;
        }

        private static Entity Resolve(EntityManager em, Entity battle, uint id)
        {
            if (id == 0)
                return Entity.Null;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CardRuntimeID>(),
                ComponentType.ReadOnly<OwnedBattle>()
            );
            using NativeArray<Entity> cards = query.ToEntityArray(Allocator.Temp);
            Entity resolved = Entity.Null;
            foreach (Entity card in cards)
                if (
                    em.GetComponentData<CardRuntimeID>(card).Value == id
                    && em.GetComponentData<OwnedBattle>(card).Battle == battle
                )
                {
                    if (resolved != Entity.Null)
                        return Entity.Null;
                    resolved = card;
                }
            return resolved;
        }
    }

    public struct SchedulerDebugAuthority : IComponentData { }
}
