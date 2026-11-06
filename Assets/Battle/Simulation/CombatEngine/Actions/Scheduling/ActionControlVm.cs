using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Data.VM;
using Archeus.Battle.VM.Execution;
using Unity.Entities;

namespace Archeus.Battle.Scheduling
{
    public static class ActionControlVm
    {
        /// <summary>A = typed operation, B = action type or stage mask, C = stable pending ID.
        /// The behaviour owner supplies the actor; source/target lineage comes from the actual event.</summary>
        public static bool Emit(
            in AbilityInstruction instruction,
            ref AbilityExecutionFrame frame,
            ref AbilityExecutionContext context,
            in BattleEvent evt
        )
        {
            if (
                instruction.A < 0
                || instruction.A > (int)ActionControlKind.OfferInterruptSelection
                || !ActionQueueService.TryActive(
                    context.Entities,
                    context.Battle,
                    out _,
                    out var active
                )
            )
                return false;
            Entity targetPlayer = Entity.Null;
            if (context.Entities.Exists(frame.Target))
            {
                if (context.Entities.HasComponent<PlayerTag>(frame.Target))
                    targetPlayer = frame.Target;
                else if (context.Entities.HasComponent<CardOwner>(frame.Target))
                    targetPlayer = context
                        .Entities.GetComponentData<CardOwner>(frame.Target)
                        .Player;
            }
            Entity actor = frame.BehaviourOwner;
            var kind = (ActionControlKind)instruction.A;
            if (kind == ActionControlKind.ExtraAction || kind == ActionControlKind.ExtraTurn)
            {
                actor = instruction.C switch
                {
                    0 => frame.BehaviourOwner,
                    1 => frame.Source,
                    2 => frame.Target,
                    3 => frame.InheritedSource,
                    _ => Entity.Null,
                };
            }
            var request = new ActionControlRequest
            {
                Kind = (ActionControlKind)instruction.A,
                ContextID = active.ContextID,
                ExecutionID = evt.ActionData.ActionExecutionID,
                FrameID = context.EmissionContext.CurrentFrameID,
                Priority = frame.ControlPriority,
                OwnerRuntimeID = frame.ControlOwnerID,
                RegistrationIndex = frame.ControlRegistration,
                TriggerIndex = frame.ControlTrigger,
                Position = instruction.C,
                RelativeQueueActionID = unchecked((uint)instruction.C),
                TargetQueueActionID = unchecked((uint)instruction.C),
                TargetPlayer = targetPlayer,
                Stages = (BattleStageMask)instruction.B,
                Action = new PlannedAction
                {
                    ActionType = PlannedActionType.CharacterAction,
                    CharacterAction = (CharacterActionType)instruction.B,
                    Source = actor,
                    PrimaryTarget = frame.Target,
                    InheritedSource = context.Entities.HasComponent<CardRuntimeID>(frame.Source)
                        ? frame.Source
                        : Entity.Null,
                    Origin =
                        (CharacterActionType)instruction.B == CharacterActionType.CounterAttack
                            ? ActionOrigin.Counterattack
                        : (CharacterActionType)instruction.B == CharacterActionType.FollowUpAttack
                            ? ActionOrigin.FollowUp
                        : ActionOrigin.Passive,
                    CausingExecutionID = evt.ActionData.ActionExecutionID,
                },
            };
            return ActionControlProcessor.Enqueue(context.Entities, context.Battle, request, out _);
        }
    }
}
