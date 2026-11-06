using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Scheduling;
using Unity.Entities;

namespace Archeus.Battle.Runtime
{
    /// <summary>Synchronous commits owned by BattlePhaseTransitionSystem. Call only outside live queries.</summary>
    public static class AttackingStageExecutionManager
    {
        public static void ClearCompletionTags(EntityManager em, Entity battle)
        {
            em.RemoveComponent<BattleTurnStartCompleteTag>(battle);
            em.RemoveComponent<BattleDrawingCompleteTag>(battle);
            em.RemoveComponent<BattlePlanningCompleteTag>(battle);
            em.RemoveComponent<BattleAttackingCompleteTag>(battle);
            em.RemoveComponent<BattleTurnEndCompleteTag>(battle);
            em.RemoveComponent<EffectDurationsProcessingTag>(battle);
        }

        public static bool SetPhase(EntityManager em, Entity battle, BattlePhase phase)
        {
            if (!em.HasComponent<BattlePhaseEpoch>(battle))
                return false;
            var epoch = em.GetComponentData<BattlePhaseEpoch>(battle);
            if (epoch.Value == uint.MaxValue)
            {
                ActionQueueService.Fail(em, battle, SchedulerFailure.IdentityExhausted);
                return false;
            }
            ClearCompletionTags(em, battle);
            epoch.Value++;
            em.SetComponentData(battle, epoch);
            em.SetComponentData(battle, new BattleState { Phase = phase });
            return true;
        }

        private static bool Context(
            EntityManager em,
            Entity entity,
            Entity battle,
            out AttackingStageExecutionContext data
        )
        {
            data = default;
            if (
                !ActionQueueService.Belongs(em, entity, battle)
                || !em.HasComponent<AttackingStageExecutionContext>(entity)
                || !em.HasBuffer<PlannedAction>(entity)
            )
                return false;
            data = em.GetComponentData<AttackingStageExecutionContext>(entity);
            return data.Battle == battle;
        }

        public static bool TryActivateRoot(EntityManager em, Entity battle, Entity root)
        {
            if (
                !em.Exists(battle)
                || !em.HasComponent<AttackingStageExecutionInstance>(battle)
                || !em.HasComponent<BattlePhaseEpoch>(battle)
                || em.GetComponentData<BattlePhaseEpoch>(battle).Value == uint.MaxValue
                || em.GetComponentData<AttackingStageExecutionInstance>(battle).Context
                    != Entity.Null
                || !Context(em, root, battle, out var data)
                || data.ParentContext != Entity.Null
                || data.ExecutionType != AttackingStageExecutionType.NormalTurn
                || data.ExecutionStatus != AttackingStageExecutionStatus.Created
            )
                return false;
            BattlePhase phase = em.GetComponentData<BattleState>(battle).Phase;
            if (phase != BattlePhase.Setup && phase != BattlePhase.TurnEnd)
                return false;
            data.ExecutionStatus = AttackingStageExecutionStatus.Active;
            em.SetComponentData(root, data);
            em.SetComponentData(battle, new AttackingStageExecutionInstance { Context = root });
            SetActivePlayer(em, battle, data.ActingPlayer);
            return SetPhase(em, battle, BattlePhase.TurnStart);
        }

        public static bool TryBeginChild(EntityManager em, Entity battle, Entity child)
        {
            if (
                !ActionQueueService.IsSettled(em, battle)
                || !em.HasComponent<BattlePhaseEpoch>(battle)
                || em.GetComponentData<BattlePhaseEpoch>(battle).Value == uint.MaxValue
                || em.GetComponentData<BattleState>(battle).Phase != BattlePhase.Attacking
                || !ActionQueueService.TryActive(em, battle, out Entity parent, out var parentData)
                || !Context(em, child, battle, out var childData)
                || childData.ParentContext != parent
                || childData.ExecutionType == AttackingStageExecutionType.NormalTurn
                || childData.ExecutionStatus != AttackingStageExecutionStatus.Created
            )
                return false;
            parentData.DeferredPhase = BattlePhase.Attacking;
            parentData.RemainingActionPoints = em.GetComponentData<RemainingActionPoints>(
                parentData.ActingPlayer
            ).Value;
            parentData.ExecutionStatus = AttackingStageExecutionStatus.Suspended;
            childData.ExecutionStatus = AttackingStageExecutionStatus.Active;
            childData.RemainingActionPoints =
                childData.ExecutionType == AttackingStageExecutionType.ExtraAction ? 1 : 4;
            em.SetComponentData(parent, parentData);
            em.SetComponentData(child, childData);
            em.SetComponentData(
                childData.ActingPlayer,
                new RemainingActionPoints { Value = childData.RemainingActionPoints }
            );
            em.SetComponentData(battle, new AttackingStageExecutionInstance { Context = child });
            SetActivePlayer(em, battle, childData.ActingPlayer);
            ActionOrderPublication.EmitContext(
                em,
                battle,
                SchedulerFactKind.ContextSuspended,
                parentData
            );
            ActionOrderPublication.EmitContext(
                em,
                battle,
                SchedulerFactKind.ContextActivated,
                childData
            );
            return SetPhase(
                em,
                battle,
                childData.ExecutionType == AttackingStageExecutionType.ExtraAction
                    ? BattlePhase.Planning
                    : BattlePhase.Drawing
            );
        }

        public static bool TryCompleteChildAndResume(EntityManager em, Entity battle)
        {
            if (
                !ActionQueueService.IsSettled(em, battle)
                || !em.HasComponent<BattlePhaseEpoch>(battle)
                || em.GetComponentData<BattlePhaseEpoch>(battle).Value == uint.MaxValue
                || em.GetComponentData<BattleState>(battle).Phase != BattlePhase.Attacking
                || !ActionQueueService.TryActive(em, battle, out Entity child, out var data)
                || data.ExecutionType == AttackingStageExecutionType.NormalTurn
                || em.GetBuffer<PlannedAction>(child).Length != 0
                || !Context(em, data.ParentContext, battle, out var parent)
                || parent.ExecutionStatus != AttackingStageExecutionStatus.Suspended
            )
                return false;
            data.RemainingActionPoints = em.GetComponentData<RemainingActionPoints>(
                data.ActingPlayer
            ).Value;
            data.ExecutionStatus = AttackingStageExecutionStatus.Completed;
            if (em.HasBuffer<ArmedActionControl>(battle))
            {
                var armed = em.GetBuffer<ArmedActionControl>(battle);
                for (int i = armed.Length - 1; i >= 0; i--)
                    if (armed[i].Request.ContextID == data.ContextID)
                        armed.RemoveAt(i);
            }
            if (em.HasBuffer<PendingStageSkip>(battle))
            {
                var skips = em.GetBuffer<PendingStageSkip>(battle);
                for (int i = skips.Length - 1; i >= 0; i--)
                    if (skips[i].ContextID == data.ContextID)
                        skips.RemoveAt(i);
            }
            parent.ExecutionStatus = AttackingStageExecutionStatus.Active;
            em.SetComponentData(child, data);
            em.SetComponentData(data.ParentContext, parent);
            em.SetComponentData(
                parent.ActingPlayer,
                new RemainingActionPoints { Value = parent.RemainingActionPoints }
            );
            em.SetComponentData(
                battle,
                new AttackingStageExecutionInstance { Context = data.ParentContext }
            );
            SetActivePlayer(em, battle, parent.ActingPlayer);
            ActionOrderPublication.EmitContext(
                em,
                battle,
                SchedulerFactKind.ContextCompleted,
                data
            );
            ActionOrderPublication.EmitContext(
                em,
                battle,
                SchedulerFactKind.ContextResumed,
                parent
            );
            return SetPhase(em, battle, BattlePhase.Attacking);
        }

        public static bool TryCompleteRoot(EntityManager em, Entity battle)
        {
            if (
                em.GetComponentData<BattleState>(battle).Phase != BattlePhase.TurnEnd
                || !ActionQueueService.IsSettled(em, battle)
                || !ActionQueueService.TryActive(em, battle, out Entity root, out var data)
                || data.ExecutionType != AttackingStageExecutionType.NormalTurn
            )
                return false;
            data.ExecutionStatus = AttackingStageExecutionStatus.Completed;
            em.SetComponentData(root, data);
            em.SetComponentData(
                battle,
                new AttackingStageExecutionInstance { Context = Entity.Null }
            );
            ActionOrderPublication.EmitContext(
                em,
                battle,
                SchedulerFactKind.ContextCompleted,
                data
            );
            return true;
        }

        private static void SetActivePlayer(EntityManager em, Entity battle, Entity player)
        {
            if (em.HasComponent<ActiveTurnPlayer>(battle))
                em.SetComponentData(battle, new ActiveTurnPlayer { Player = player });
            else
                em.AddComponentData(battle, new ActiveTurnPlayer { Player = player });
        }
    }
}
