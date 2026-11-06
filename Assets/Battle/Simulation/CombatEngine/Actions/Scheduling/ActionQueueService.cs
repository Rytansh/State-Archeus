using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Presentation.State;
using Unity.Entities;

namespace Archeus.Battle.Scheduling
{
    internal struct EcsActionQueue : IActionQueueStorage<PlannedAction>
    {
        private DynamicBuffer<PlannedAction> buffer;

        public EcsActionQueue(DynamicBuffer<PlannedAction> buffer) => this.buffer = buffer;

        public int Count => buffer.Length;
        public PlannedAction this[int index]
        {
            get => buffer[index];
            set => buffer[index] = value;
        }

        public void Add(PlannedAction value) => buffer.Add(value);

        public void RemoveAt(int index) => buffer.RemoveAt(index);
    }

    public static class ActionQueueService
    {
        public static void AddBattleData(EntityCommandBuffer ecb, Entity battle)
        {
            ecb.AddComponent(
                battle,
                new ActionSchedulerCounters
                {
                    NextQueueActionID = 1,
                    NextControlID = 1,
                    NextSelectionID = 1,
                }
            );
            ecb.AddComponent<ActionSchedulerState>(battle);
            ecb.AddBuffer<ActionControlRequest>(battle);
            ecb.AddBuffer<ActionControlResult>(battle);
            ecb.AddBuffer<ArmedActionControl>(battle);
            ecb.AddBuffer<ActionOutcomeRecord>(battle);
            ecb.AddBuffer<PendingContextTransition>(battle);
            ecb.AddBuffer<PendingStageSkip>(battle);
            ecb.AddBuffer<PendingInterruptSelection>(battle);
        }

        public static bool TryActive(
            EntityManager em,
            Entity battle,
            out Entity context,
            out AttackingStageExecutionContext data
        )
        {
            context = Entity.Null;
            data = default;
            if (!em.Exists(battle) || !em.HasComponent<AttackingStageExecutionInstance>(battle))
                return false;
            context = em.GetComponentData<AttackingStageExecutionInstance>(battle).Context;
            if (
                !em.Exists(context)
                || !em.HasComponent<AttackingStageExecutionContext>(context)
                || !em.HasComponent<OwnedBattle>(context)
                || em.GetComponentData<OwnedBattle>(context).Battle != battle
            )
                return false;
            data = em.GetComponentData<AttackingStageExecutionContext>(context);
            return data.Battle == battle
                && data.ExecutionStatus == AttackingStageExecutionStatus.Active;
        }

        public static bool IsSettled(EntityManager em, Entity battle)
        {
            return (
                    !em.HasBuffer<ActionExecutionState>(battle)
                    || em.GetBuffer<ActionExecutionState>(battle).Length == 0
                )
                && (
                    !em.HasBuffer<BattleEvent>(battle)
                    || em.GetBuffer<BattleEvent>(battle).Length == 0
                )
                && (
                    !em.HasBuffer<ChainedBattleEvent>(battle)
                    || em.GetBuffer<ChainedBattleEvent>(battle).Length == 0
                )
                && (
                    !em.HasBuffer<BehaviourExecutionRequest>(battle)
                    || em.GetBuffer<BehaviourExecutionRequest>(battle).Length == 0
                );
        }

        public static bool Healthy(EntityManager em, Entity battle) =>
            em.HasComponent<ActionSchedulerState>(battle)
            && em.GetComponentData<ActionSchedulerState>(battle).Failure == SchedulerFailure.None;

        public static void Fail(EntityManager em, Entity battle, SchedulerFailure reason)
        {
            if (!em.HasComponent<ActionSchedulerState>(battle))
                return;
            var state = em.GetComponentData<ActionSchedulerState>(battle);
            if (state.Failure != SchedulerFailure.None)
                return;
            state.Failure = reason;
            em.SetComponentData(battle, state);
            ActionOrderPublication.Emit(
                em,
                battle,
                SchedulerFactKind.Failed,
                default,
                ActionOutcome.Failed
            );
        }

        public static bool Belongs(EntityManager em, Entity entity, Entity battle) =>
            em.Exists(entity)
            && em.HasComponent<OwnedBattle>(entity)
            && em.GetComponentData<OwnedBattle>(entity).Battle == battle;

        public static bool IsOnField(EntityManager em, Entity source, Entity player)
        {
            if (!em.HasBuffer<FieldCard>(player))
                return false;
            foreach (FieldCard field in em.GetBuffer<FieldCard>(player))
                if (field.Card == source)
                    return true;
            return false;
        }

        public static bool IsLivingFieldActor(EntityManager em, Entity actor, Entity battle)
        {
            return Belongs(em, actor, battle)
                && em.HasComponent<CardOwner>(actor)
                && em.HasComponent<CurrentHealth>(actor)
                && em.GetComponentData<CurrentHealth>(actor).Value > 0
                && !em.HasComponent<Dead>(actor)
                && ValidOwnerAndSide(
                    em,
                    actor,
                    em.GetComponentData<CardOwner>(actor).Player,
                    battle
                )
                && IsOnField(em, actor, em.GetComponentData<CardOwner>(actor).Player);
        }

        public static bool CanReceiveExtraAction(EntityManager em, Entity actor, Entity battle)
        {
            return IsLivingFieldActor(em, actor, battle)
                && (
                    !em.HasComponent<ActionEligibility>(actor)
                    || em.GetComponentData<ActionEligibility>(actor).ManualMask != 0
                );
        }

        private static bool ValidOwnerAndSide(
            EntityManager em,
            Entity card,
            Entity owner,
            Entity battle
        )
        {
            return Belongs(em, owner, battle)
                && em.HasComponent<PlayerTag>(owner)
                && em.HasComponent<BattleSideAssignment>(owner)
                && em.HasComponent<BattleSideAssignment>(card)
                && em.GetComponentData<BattleSideAssignment>(owner).Side
                    == em.GetComponentData<BattleSideAssignment>(card).Side;
        }

        public static bool Definition(
            EntityManager em,
            PlannedAction action,
            out ActionProgramBinding definition
        )
        {
            definition = default;
            if (em.HasBuffer<ActionProgramBinding>(action.Source))
                foreach (var binding in em.GetBuffer<ActionProgramBinding>(action.Source))
                    if (
                        binding.Type == action.CharacterAction
                        && binding.DefinitionID == action.DefinitionID
                    )
                    {
                        definition = binding;
                        return binding.ProgramIndex >= 0 && binding.EnergyCost >= 0;
                    }
            if (
                action.DefinitionID != 0
                || (
                    action.CharacterAction != CharacterActionType.NormalAttack
                    && action.CharacterAction != CharacterActionType.SuperchargedAttack
                )
            )
                return false;
            definition = new ActionProgramBinding
            {
                Type = action.CharacterAction,
                ProgramIndex = -1,
                TargetMode = ActionTargetMode.SingleEnemy,
                EnergyCost =
                    action.CharacterAction == CharacterActionType.SuperchargedAttack ? 5 : 0,
            };
            return em.HasComponent<CharacterRuntimeStatBaseline>(action.Source);
        }

        public static bool Validate(
            EntityManager em,
            Entity battle,
            AttackingStageExecutionContext context,
            ref PlannedAction action,
            bool manual,
            out ActionProgramBinding definition
        )
        {
            definition = default;
            if (
                action.InheritedSource != Entity.Null
                && !Belongs(em, action.InheritedSource, battle)
            )
                return false;
            if (
                action.ActionType != PlannedActionType.CharacterAction
                || (
                    action.CharacterAction == CharacterActionType.None
                    || action.CharacterAction > CharacterActionType.FollowUpAttack
                )
                || !Belongs(em, action.Source, battle)
                || !em.HasComponent<CardRuntimeID>(action.Source)
                || !em.HasComponent<CardOwner>(action.Source)
                || !em.HasComponent<CurrentHealth>(action.Source)
                || em.GetComponentData<CurrentHealth>(action.Source).Value <= 0
                || em.HasComponent<Dead>(action.Source)
            )
                return false;
            Entity owner = em.GetComponentData<CardOwner>(action.Source).Player;
            if (
                !ValidOwnerAndSide(em, action.Source, owner, battle)
                || !IsOnField(em, action.Source, owner)
                || em.GetComponentData<CardRuntimeID>(action.Source).Value == 0
            )
                return false;
            if (
                manual
                && (
                    owner != context.ActingPlayer
                    || action.SubmittedByPlayer != owner
                    || (
                        context.ExecutionType == AttackingStageExecutionType.ExtraAction
                        && context.ActingCharacter != action.Source
                    )
                )
            )
                return false;
            if (
                !manual
                && action.SubmittedByPlayer != Entity.Null
                && action.SubmittedByPlayer != owner
            )
                return false;
            if (em.HasComponent<ActionEligibility>(action.Source))
            {
                var eligibility = em.GetComponentData<ActionEligibility>(action.Source);
                uint mask = manual ? eligibility.ManualMask : eligibility.AutomaticMask;
                if ((mask & ActionEligibility.Mask(action.CharacterAction)) == 0)
                    return false;
            }
            if (!Definition(em, action, out definition))
                return false;
            if (definition.ProgramIndex >= 0)
            {
                if (!em.HasComponent<BattleContentRegistry>(battle))
                    return false;
                var registry = em.GetComponentData<BattleContentRegistry>(
                    battle
                ).BattleRegistryReference;
                if (
                    !registry.IsCreated
                    || definition.ProgramIndex >= registry.Value.AbilityPrograms.Length
                )
                    return false;
            }
            if (definition.TargetMode > ActionTargetMode.SingleAlly)
                return false;
            action.TargetMode = definition.TargetMode;
            action.SubmittedByPlayer = owner;
            if (
                definition.EnergyCost > 0
                && (
                    !em.HasComponent<HeldSuperchargedEnergy>(action.Source)
                    || em.GetComponentData<HeldSuperchargedEnergy>(action.Source).Value
                        < definition.EnergyCost
                )
            )
                return false;
            if (
                action.TargetMode == ActionTargetMode.SingleEnemy
                || action.TargetMode == ActionTargetMode.SingleAlly
            )
            {
                Entity target = action.PrimaryTarget;
                if (
                    !Belongs(em, target, battle)
                    || !em.HasComponent<CardOwner>(target)
                    || !em.HasComponent<CurrentHealth>(target)
                    || em.GetComponentData<CurrentHealth>(target).Value <= 0
                    || em.HasComponent<Dead>(target)
                    || !IsOnField(em, target, em.GetComponentData<CardOwner>(target).Player)
                )
                    return false;
                Entity targetOwner = em.GetComponentData<CardOwner>(target).Player;
                if (
                    !ValidOwnerAndSide(em, target, targetOwner, battle)
                    || !em.HasComponent<CardRuntimeID>(target)
                    || em.GetComponentData<CardRuntimeID>(target).Value == 0
                )
                    return false;
                bool ally = targetOwner == owner;
                if (ally != (action.TargetMode == ActionTargetMode.SingleAlly))
                    return false;
                if (
                    manual
                    && action.TargetMode == ActionTargetMode.SingleEnemy
                    && em.HasBuffer<ActionTargetHistory>(contextEntity(em, battle))
                )
                    foreach (
                        var previous in em.GetBuffer<ActionTargetHistory>(contextEntity(em, battle))
                    )
                        if (previous.Source == action.Source && previous.Target == target)
                            return false;
            }
            if (action.TargetMode == ActionTargetMode.Self)
                action.PrimaryTarget = action.Source;
            if (
                action.TargetMode == ActionTargetMode.None
                || action.TargetMode == ActionTargetMode.AllEnemies
            )
                action.PrimaryTarget = Entity.Null;
            return true;
        }

        private static Entity contextEntity(EntityManager em, Entity battle) =>
            em.GetComponentData<AttackingStageExecutionInstance>(battle).Context;

        public static bool TryPlan(
            EntityManager em,
            Entity battle,
            Entity player,
            PlannedAction action,
            out uint id
        )
        {
            id = 0;
            if (
                !Healthy(em, battle)
                || em.GetComponentData<BattleState>(battle).Phase != BattlePhase.Planning
                || Archeus.Battle.Systems.Setup.BattlePhaseTransitionSystem.ShouldSkip(
                    em,
                    battle,
                    BattlePhase.Planning
                )
                || em.HasComponent<BattlePlanningCompleteTag>(battle)
                || !em.HasComponent<Archeus.Battle.Snapshots.StageSnapshotStatus>(battle)
                || !em.GetComponentData<Archeus.Battle.Snapshots.StageSnapshotStatus>(
                    battle
                ).IsValid
                || !TryActive(em, battle, out Entity context, out var data)
                || data.ActingPlayer != player
                || !em.HasComponent<RemainingActionPoints>(player)
                || em.GetComponentData<RemainingActionPoints>(player).Value <= 0
            )
                return false;
            action.SubmittedByPlayer = player;
            action.Origin = ActionOrigin.PlayerPlanned;
            action.CausingExecutionID = 0;
            action.InheritedSource = Entity.Null;
            if (!Validate(em, battle, data, ref action, true, out _))
                return false;
            var buffer = em.GetBuffer<PlannedAction>(context);
            if (data.ExecutionType == AttackingStageExecutionType.ExtraAction)
                foreach (var existing in buffer)
                    if (existing.Origin == ActionOrigin.PlayerPlanned)
                        return false;
            foreach (var pending in buffer)
                if (
                    action.TargetMode == ActionTargetMode.SingleEnemy
                    && pending.Source == action.Source
                    && pending.PrimaryTarget == action.PrimaryTarget
                )
                    return false;
            action.ReservedAP = 1;
            if (
                !TryInsert(
                    em,
                    battle,
                    context,
                    action,
                    buffer.Length,
                    SchedulerFactKind.Enqueued,
                    out id
                )
            )
                return false;
            var ap = em.GetComponentData<RemainingActionPoints>(player);
            ap.Value--;
            em.SetComponentData(player, ap);
            data.RemainingActionPoints = ap.Value;
            em.SetComponentData(context, data);
            return true;
        }

        internal static bool TryInsert(
            EntityManager em,
            Entity battle,
            Entity context,
            PlannedAction action,
            int position,
            SchedulerFactKind fact,
            out uint id,
            bool publish = true
        )
        {
            id = 0;
            if (
                !Healthy(em, battle)
                || !TryActive(em, battle, out Entity active, out _)
                || active != context
            )
                return false;
            var counter = em.GetComponentData<ActionSchedulerCounters>(battle);
            if (counter.NextQueueActionID == 0 || counter.NextQueueActionID == uint.MaxValue)
            {
                Fail(em, battle, SchedulerFailure.IdentityExhausted);
                return false;
            }
            action.QueueActionID = counter.NextQueueActionID;
            action.ContextID = em.GetComponentData<AttackingStageExecutionContext>(
                context
            ).ContextID;
            var storage = new EcsActionQueue(em.GetBuffer<PlannedAction>(context));
            if (
                !ActionQueueAlgorithms.Insert<PlannedAction, EcsActionQueue>(
                    ref storage,
                    action,
                    position
                )
            )
                return false;
            counter.NextQueueActionID++;
            em.SetComponentData(battle, counter);
            id = action.QueueActionID;
            if (publish)
                ActionOrderPublication.Emit(em, battle, fact, action);
            return true;
        }

        public static bool TryRemove(
            EntityManager em,
            Entity battle,
            Entity context,
            uint id,
            bool refund
        )
        {
            if (
                !Healthy(em, battle)
                || !TryActive(em, battle, out Entity active, out _)
                || active != context
            )
                return false;
            var storage = new EcsActionQueue(em.GetBuffer<PlannedAction>(context));
            if (
                !ActionQueueAlgorithms.Remove<PlannedAction, EcsActionQueue>(
                    ref storage,
                    id,
                    out var removed
                )
            )
                return false;
            if (refund && removed.Origin == ActionOrigin.PlayerPlanned)
            {
                var data = em.GetComponentData<AttackingStageExecutionContext>(context);
                var ap = em.GetComponentData<RemainingActionPoints>(data.ActingPlayer);
                ap.Value += removed.ReservedAP;
                data.RemainingActionPoints = ap.Value;
                em.SetComponentData(data.ActingPlayer, ap);
                em.SetComponentData(context, data);
            }
            ActionOrderPublication.Emit(
                em,
                battle,
                SchedulerFactKind.Removed,
                removed,
                ActionOutcome.Cancelled
            );
            return true;
        }
    }
}
