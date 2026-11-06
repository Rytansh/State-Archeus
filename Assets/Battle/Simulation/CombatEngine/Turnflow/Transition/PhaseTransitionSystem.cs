using System;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Runtime;
using Archeus.Battle.Scheduling;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Setup
{
    /// <summary>The sole runtime owner of phase/context/player transitions. Structural work uses copied entity arrays.</summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleRootGroup))]
    [UpdateAfter(typeof(BattleSimulationGroup))]
    public partial struct BattlePhaseTransitionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<BattleTag>(),
                ComponentType.ReadOnly<BattleState>(),
                ComponentType.ReadOnly<AttackingStageExecutionInstance>(),
                ComponentType.ReadOnly<ActionSchedulerState>()
            );
            using NativeArray<Entity> battles = query.ToEntityArray(Allocator.Temp);
            foreach (Entity battle in battles)
                Advance(em, battle);
        }

        // Stage systems consult the same policy before doing work; the controller commits the
        // actual transition after causal work. This also handles a skip received this input update.
        public static bool ShouldSkip(EntityManager em, Entity battle, BattlePhase phase)
        {
            if (!ActionQueueService.TryActive(em, battle, out _, out var data))
                return false;
            if (
                phase == BattlePhase.Planning
                && em.HasComponent<SkipPlanningStageTag>(data.ActingPlayer)
            )
                return true;
            if (
                phase == BattlePhase.Attacking
                && em.HasComponent<SkipAttackingStageTag>(data.ActingPlayer)
            )
                return true;
            if (!em.HasBuffer<PendingStageSkip>(battle))
                return false;
            BattleStageMask bit = Mask(phase);
            foreach (var skip in em.GetBuffer<PendingStageSkip>(battle))
                if (
                    skip.Player == data.ActingPlayer
                    && (skip.Stages & bit) != 0
                    && (
                        skip.ContextID == data.ContextID
                        || (
                            skip.ContextID == 0
                            && data.ExecutionType == AttackingStageExecutionType.NormalTurn
                        )
                    )
                )
                    return true;
            return false;
        }

        private static void Advance(EntityManager em, Entity battle)
        {
            if (!ActionQueueService.Healthy(em, battle))
                return;
            var phase = em.GetComponentData<BattleState>(battle).Phase;
            if (phase == BattlePhase.Setup)
            {
                if (
                    !em.HasComponent<BattleReadyTag>(battle)
                    || !em.HasComponent<ActiveTurnPlayer>(battle)
                )
                    return;
                if (StartRoot(em, battle, em.GetComponentData<ActiveTurnPlayer>(battle).Player))
                {
                    em.RemoveComponent<BattleReadyTag>(battle);
                    ApplySkippedStages(em, battle);
                }
                return;
            }
            if (!ActionQueueService.TryActive(em, battle, out Entity context, out var data))
                return;
            if (
                phase == BattlePhase.Attacking
                && ActionQueueService.IsSettled(em, battle)
                && BeginQueuedChild(em, battle, context)
            )
                return;
            if (ApplySkippedStages(em, battle))
                return;
            phase = em.GetComponentData<BattleState>(battle).Phase;
            if (!ActionQueueService.TryActive(em, battle, out context, out data))
                return;
            switch (phase)
            {
                case BattlePhase.TurnStart:
                    if (
                        data.ExecutionType == AttackingStageExecutionType.NormalTurn
                        && em.HasComponent<BattleTurnStartCompleteTag>(battle)
                        && ActionQueueService.IsSettled(em, battle)
                    )
                    {
                        SyncAP(em, context);
                        Change(em, battle, BattlePhase.Drawing);
                    }
                    break;
                case BattlePhase.Drawing:
                    if (em.HasComponent<BattleDrawingCompleteTag>(battle))
                    {
                        SyncAP(em, context);
                        Change(em, battle, BattlePhase.Planning);
                    }
                    break;
                case BattlePhase.Planning:
                    if (
                        em.HasComponent<BattlePlanningCompleteTag>(battle)
                        && !HasSelections(em, battle, data.ContextID)
                        && ActionQueueService.IsSettled(em, battle)
                    )
                    {
                        SyncAP(em, context);
                        Change(em, battle, BattlePhase.Attacking);
                    }
                    break;
                case BattlePhase.Attacking:
                    if (
                        !em.HasComponent<BattleAttackingCompleteTag>(battle)
                        || !ActionQueueService.IsSettled(em, battle)
                        || em.GetBuffer<PlannedAction>(context).Length != 0
                        || ActionControlProcessor.HasPendingChild(em, battle, context)
                        || em.GetBuffer<ActionControlRequest>(battle).Length != 0
                    )
                        break;
                    if (data.ExecutionType == AttackingStageExecutionType.NormalTurn)
                        Change(em, battle, BattlePhase.TurnEnd);
                    else
                        AttackingStageExecutionManager.TryCompleteChildAndResume(em, battle);
                    break;
                case BattlePhase.TurnEnd:
                    if (
                        data.ExecutionType == AttackingStageExecutionType.NormalTurn
                        && em.HasComponent<BattleTurnEndCompleteTag>(battle)
                        && ActionQueueService.IsSettled(em, battle)
                    )
                        FinishNormal(em, battle, data.ActingPlayer);
                    break;
            }
        }

        private static void Change(EntityManager em, Entity battle, BattlePhase next)
        {
            if (AttackingStageExecutionManager.SetPhase(em, battle, next))
                ApplySkippedStages(em, battle);
        }

        private static bool StartRoot(EntityManager em, Entity battle, Entity player)
        {
            if (
                !AttackingStageExecutionContextFactory.TryCreate(
                    em,
                    battle,
                    player,
                    AttackingStageExecutionType.NormalTurn,
                    Entity.Null,
                    Entity.Null,
                    out Entity root
                )
            )
            {
                ActionQueueService.Fail(em, battle, SchedulerFailure.IdentityExhausted);
                return false;
            }
            var state = em.GetComponentData<ActionSchedulerState>(battle);
            state.DispatchesThisTurn = 0;
            state.ControlsThisTurn = 0;
            state.RetiredControlID =
                em.GetComponentData<ActionSchedulerCounters>(battle).NextControlID - 1;
            em.SetComponentData(battle, state);
            em.GetBuffer<ActionControlResult>(battle).Clear();
            if (!AttackingStageExecutionManager.TryActivateRoot(em, battle, root))
            {
                em.DestroyEntity(root);
                ActionQueueService.Fail(em, battle, SchedulerFailure.IdentityExhausted);
                return false;
            }
            ActionOrderPublication.EmitContext(
                em,
                battle,
                SchedulerFactKind.ContextActivated,
                em.GetComponentData<AttackingStageExecutionContext>(root)
            );
            return true;
        }

        private static void FinishNormal(EntityManager em, Entity battle, Entity previous)
        {
            if (!AttackingStageExecutionManager.TryCompleteRoot(em, battle))
                return;
            em.GetBuffer<ArmedActionControl>(battle).Clear();
            var skips = em.GetBuffer<PendingStageSkip>(battle);
            for (int i = skips.Length - 1; i >= 0; i--)
                if (skips[i].Player == previous)
                    skips.RemoveAt(i);
            Entity next = Entity.Null;
            if (em.HasBuffer<BattlePlayerReference>(battle))
                foreach (var candidate in em.GetBuffer<BattlePlayerReference>(battle))
                    if (
                        candidate.Player != previous
                        && ActionQueueService.Belongs(em, candidate.Player, battle)
                    )
                    {
                        if (next != Entity.Null)
                        {
                            ActionQueueService.Fail(
                                em,
                                battle,
                                SchedulerFailure.EventEngineUnsettled
                            );
                            return;
                        }
                        next = candidate.Player;
                    }
            if (next == Entity.Null)
            {
                ActionQueueService.Fail(em, battle, SchedulerFailure.EventEngineUnsettled);
                return;
            }
            // A completed context has no mutable external handle contract. Facts retain stable IDs.
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
                        data.Battle == battle
                        && data.ExecutionStatus == AttackingStageExecutionStatus.Completed
                    )
                        em.DestroyEntity(entity);
                }
            if (StartRoot(em, battle, next))
                ApplySkippedStages(em, battle);
        }

        private static bool BeginQueuedChild(EntityManager em, Entity battle, Entity parent)
        {
            var requests = em.GetBuffer<PendingContextTransition>(battle);
            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                if (request.Parent != parent)
                    continue;
                requests.RemoveAt(i);
                if (
                    ActionQueueService.IsLivingFieldActor(em, request.Character, battle)
                    && AttackingStageExecutionContextFactory.TryCreate(
                        em,
                        battle,
                        request.Player,
                        request.Type,
                        parent,
                        request.Character,
                        out Entity child
                    )
                )
                {
                    if (AttackingStageExecutionManager.TryBeginChild(em, battle, child))
                        return true;
                    em.DestroyEntity(child);
                }
                // Reacquire buffers after structural creation. A no-longer-eligible actor cannot open an unusable child window.
                requests = em.GetBuffer<PendingContextTransition>(battle);
                i--;
            }
            return false;
        }

        private static bool ApplySkippedStages(EntityManager em, Entity battle)
        {
            bool changed = false;
            // At most five stages can be skipped in one context. Do not loop into another normal turn here.
            for (int step = 0; step < 5; step++)
            {
                if (
                    !ActionQueueService.TryActive(em, battle, out Entity context, out var data)
                    || !ActionQueueService.IsSettled(em, battle)
                )
                    break;
                BattlePhase phase = em.GetComponentData<BattleState>(battle).Phase;
                BattleStageMask bit = Mask(phase);
                var skips = em.GetBuffer<PendingStageSkip>(battle);
                bool skip = false;
                for (int i = skips.Length - 1; i >= 0; i--)
                {
                    var entry = skips[i];
                    bool scope =
                        entry.ContextID == data.ContextID
                        || (
                            entry.ContextID == 0
                            && data.ExecutionType == AttackingStageExecutionType.NormalTurn
                        );
                    if (!scope || entry.Player != data.ActingPlayer || (entry.Stages & bit) == 0)
                        continue;
                    skip = true;
                    entry.Stages &= ~bit;
                    if (entry.Stages == BattleStageMask.None)
                        skips.RemoveAt(i);
                    else
                        skips[i] = entry;
                }
                // Existing first-turn tie rules are consumed centrally rather than by phase writers.
                if (
                    phase == BattlePhase.Planning
                    && em.HasComponent<SkipPlanningStageTag>(data.ActingPlayer)
                )
                {
                    em.RemoveComponent<SkipPlanningStageTag>(data.ActingPlayer);
                    skip = true;
                }
                if (
                    phase == BattlePhase.Attacking
                    && em.HasComponent<SkipAttackingStageTag>(data.ActingPlayer)
                )
                {
                    em.RemoveComponent<SkipAttackingStageTag>(data.ActingPlayer);
                    skip = true;
                }
                if (!skip)
                    break;
                changed = true;
                ActionOrderPublication.EmitContext(
                    em,
                    battle,
                    SchedulerFactKind.StageSkipped,
                    data
                );
                if (phase == BattlePhase.Planning || phase == BattlePhase.Attacking)
                {
                    var queue = em.GetBuffer<PlannedAction>(context);
                    while (queue.Length != 0)
                        ActionQueueService.TryRemove(
                            em,
                            battle,
                            context,
                            queue[0].QueueActionID,
                            false
                        );
                    var offers = em.GetBuffer<PendingInterruptSelection>(battle);
                    for (int i = offers.Length - 1; i >= 0; i--)
                        if (offers[i].ContextID == data.ContextID)
                            offers.RemoveAt(i);
                }
                if (phase == BattlePhase.TurnStart)
                {
                    // A skipped normal turn still consumes a counted turn, but no start effects/AP refresh.
                    if (!em.HasComponent<BattleTurnStartCompleteTag>(battle))
                    {
                        var counter = em.GetComponentData<TurnCounter>(battle);
                        counter.CurrentTurn++;
                        em.SetComponentData(battle, counter);
                    }
                    AttackingStageExecutionManager.SetPhase(em, battle, BattlePhase.Drawing);
                }
                else if (phase == BattlePhase.Drawing)
                    AttackingStageExecutionManager.SetPhase(em, battle, BattlePhase.Planning);
                else if (phase == BattlePhase.Planning)
                    AttackingStageExecutionManager.SetPhase(em, battle, BattlePhase.Attacking);
                else if (phase == BattlePhase.Attacking)
                {
                    if (data.ExecutionType != AttackingStageExecutionType.NormalTurn)
                    {
                        AttackingStageExecutionManager.TryCompleteChildAndResume(em, battle);
                        return true;
                    }
                    AttackingStageExecutionManager.SetPhase(em, battle, BattlePhase.TurnEnd);
                }
                else if (phase == BattlePhase.TurnEnd)
                {
                    FinishNormal(em, battle, data.ActingPlayer);
                    return true;
                }
            }
            return changed;
        }

        private static bool HasSelections(EntityManager em, Entity battle, uint contextID)
        {
            var offers = em.GetBuffer<PendingInterruptSelection>(battle);
            if (
                ActionQueueService.TryActive(em, battle, out Entity context, out _)
                && em.GetBuffer<PlannedAction>(context).Length == 0
            )
                for (int i = offers.Length - 1; i >= 0; i--)
                    if (offers[i].ContextID == contextID)
                        offers.RemoveAt(i);
            uint epoch = em.GetComponentData<BattlePhaseEpoch>(battle).Value;
            for (int i = offers.Length - 1; i >= 0; i--)
                if (offers[i].ContextID == contextID && offers[i].PhaseEpoch != epoch)
                    offers.RemoveAt(i);
            foreach (var selection in offers)
                if (selection.ContextID == contextID)
                    return true;
            return false;
        }

        private static void SyncAP(EntityManager em, Entity context)
        {
            var data = em.GetComponentData<AttackingStageExecutionContext>(context);
            data.RemainingActionPoints = em.GetComponentData<RemainingActionPoints>(
                data.ActingPlayer
            ).Value;
            em.SetComponentData(context, data);
        }

        private static BattleStageMask Mask(BattlePhase phase) =>
            phase switch
            {
                BattlePhase.TurnStart => BattleStageMask.TurnStart,
                BattlePhase.Drawing => BattleStageMask.Drawing,
                BattlePhase.Planning => BattleStageMask.Planning,
                BattlePhase.Attacking => BattleStageMask.Attacking,
                BattlePhase.TurnEnd => BattleStageMask.TurnEnd,
                _ => BattleStageMask.None,
            };
    }
}
