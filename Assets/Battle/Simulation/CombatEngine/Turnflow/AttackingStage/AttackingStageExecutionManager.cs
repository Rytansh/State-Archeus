using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
using Unity.Entities;

namespace Archeus.Battle.Runtime
{
    /// <summary>
    /// Low-level, synchronous state commits. These are NOT event/command consumers.
    /// The authoritative BattlePhaseTransitionSystem will eventually validate requests,
    /// ensure combat has settled, and invoke these operations at a safe update boundary.
    /// Each successful switch changes context, battle phase, and epoch together.
    /// </summary>
    public static class AttackingStageExecutionManager
    {
        private static bool TryReadBattle(
            EntityManager em,
            Entity battle,
            out BattleState phase,
            out AttackingStageExecutionInstance active,
            out BattlePhaseEpoch epoch
        )
        {
            phase = default;
            active = default;
            epoch = default;
            if (
                battle == Entity.Null
                || !em.Exists(battle)
                || !em.HasComponent<BattleTag>(battle)
                || !em.HasComponent<BattleState>(battle)
                || !em.HasComponent<AttackingStageExecutionInstance>(battle)
                || !em.HasComponent<BattlePhaseEpoch>(battle)
            )
                return false;

            phase = em.GetComponentData<BattleState>(battle);
            active = em.GetComponentData<AttackingStageExecutionInstance>(battle);
            epoch = em.GetComponentData<BattlePhaseEpoch>(battle);
            return epoch.Value != uint.MaxValue;
        }

        private static bool TryReadContext(
            EntityManager em,
            Entity context,
            Entity battle,
            out AttackingStageExecutionContext data
        )
        {
            data = default;
            if (
                context == Entity.Null
                || !em.Exists(context)
                || !em.HasComponent<AttackingStageExecutionContext>(context)
                || !em.HasBuffer<PlannedAction>(context)
            )
                return false;

            data = em.GetComponentData<AttackingStageExecutionContext>(context);
            return data.Battle == battle;
        }

        private static void CommitPhase(
            EntityManager em,
            Entity battle,
            BattleState phase,
            BattlePhaseEpoch epoch
        )
        {
            epoch.Value++;
            em.SetComponentData(battle, phase);
            em.SetComponentData(battle, epoch);
        }

        /// <summary>
        /// Enter a normal counted turn. Called on Setup->TurnStart or TurnEnd->TurnStart
        /// AFTER turn ownership has been determined. Does not increment TurnCounter.
        /// </summary>
        public static bool TryActivateRoot(EntityManager em, Entity battle, Entity root)
        {
            if (
                !TryReadBattle(em, battle, out var phase, out var active, out var epoch)
                || active.Context != Entity.Null
                || (phase.Phase != BattlePhase.Setup && phase.Phase != BattlePhase.TurnEnd)
                || !TryReadContext(em, root, battle, out var data)
                || data.ParentContext != Entity.Null
                || data.ExecutionType != AttackingStageExecutionType.NormalTurn
                || data.ExecutionStatus != AttackingStageExecutionStatus.Created
            )
                return false;

            data.ExecutionStatus = AttackingStageExecutionStatus.Active;
            active.Context = root;
            phase.Phase = BattlePhase.TurnStart;
            em.SetComponentData(root, data);
            em.SetComponentData(battle, active);
            CommitPhase(em, battle, phase, epoch);
            return true;
        }

        /// <summary>
        /// Start an already-created child after the parent action and its event work
        /// have fully settled. The calling controller must validate that condition.
        /// </summary>
        public static bool TryBeginChild(EntityManager em, Entity battle, Entity child)
        {
            if (
                !TryReadBattle(em, battle, out var phase, out var active, out var epoch)
                || phase.Phase != BattlePhase.Attacking
                || !TryReadContext(em, active.Context, battle, out var parentData)
                || !TryReadContext(em, child, battle, out var childData)
                || active.Context == child
                || parentData.ExecutionStatus != AttackingStageExecutionStatus.Active
                || childData.ExecutionStatus != AttackingStageExecutionStatus.Created
                || childData.ParentContext != active.Context
                || childData.ExecutionType == AttackingStageExecutionType.NormalTurn
            )
                return false;

            // All validation precedes mutation. No other ECS system observes the
            // intermediate SetComponentData writes during this synchronous call.
            parentData.DeferredPhase = phase.Phase;
            parentData.ExecutionStatus = AttackingStageExecutionStatus.Suspended;
            childData.ExecutionStatus = AttackingStageExecutionStatus.Active;
            active.Context = child;
            phase.Phase =
                childData.ExecutionType == AttackingStageExecutionType.ExtraTurn
                    ? BattlePhase.Drawing
                    : BattlePhase.Planning;

            em.SetComponentData(childData.ParentContext, parentData);
            em.SetComponentData(child, childData);
            em.SetComponentData(battle, active);
            CommitPhase(em, battle, phase, epoch);
            return true;
        }

        /// <summary>
        /// Finish the active Extra Action/Turn and return to its direct parent.
        /// Caller must prove the child Attacking stage has completed, including
        /// outstanding interrupts. This helper does not inspect event queues.
        /// </summary>
        public static bool TryCompleteChildAndResume(EntityManager em, Entity battle)
        {
            if (
                !TryReadBattle(em, battle, out var phase, out var active, out var epoch)
                || phase.Phase != BattlePhase.Attacking
                || !TryReadContext(em, active.Context, battle, out var childData)
                || childData.ExecutionType == AttackingStageExecutionType.NormalTurn
                || childData.ExecutionStatus != AttackingStageExecutionStatus.Active
                || !TryReadContext(em, childData.ParentContext, battle, out var parentData)
                || parentData.ExecutionStatus != AttackingStageExecutionStatus.Suspended
                || parentData.DeferredPhase != BattlePhase.Attacking
            )
                return false;

            Entity child = active.Context;
            Entity parent = childData.ParentContext;
            childData.ExecutionStatus = AttackingStageExecutionStatus.Completed;
            parentData.ExecutionStatus = AttackingStageExecutionStatus.Active;
            active.Context = parent;
            phase.Phase = parentData.DeferredPhase;

            em.SetComponentData(parent, parentData);
            em.SetComponentData(child, childData);
            em.SetComponentData(battle, active);
            CommitPhase(em, battle, phase, epoch);
            return true;
        }

        /// <summary>
        /// Retire a completed normal turn. The controller later selects the next
        /// turn owner and creates/activates the next normal root.
        /// </summary>
        public static bool TryCompleteRoot(EntityManager em, Entity battle)
        {
            if (
                !TryReadBattle(em, battle, out var phase, out var active, out _)
                || phase.Phase != BattlePhase.TurnEnd
                || !TryReadContext(em, active.Context, battle, out var data)
                || data.ParentContext != Entity.Null
                || data.ExecutionType != AttackingStageExecutionType.NormalTurn
                || data.ExecutionStatus != AttackingStageExecutionStatus.Active
            )
                return false;

            Entity root = active.Context;
            data.ExecutionStatus = AttackingStageExecutionStatus.Completed;
            active.Context = Entity.Null;
            em.SetComponentData(root, data);
            em.SetComponentData(battle, active);
            return true;
        }
    }
}
