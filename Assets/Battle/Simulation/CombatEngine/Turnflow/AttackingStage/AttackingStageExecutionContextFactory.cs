using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
using Unity.Entities;

namespace Archeus.Battle.Runtime
{
    /// <summary>
    /// Constructs an execution context; does not activate it or change the battle phase.
    /// Call only at a safe structural-change point, not during a live SystemAPI.Query iteration.
    /// </summary>
    public static class AttackingStageExecutionContextFactory
    {
        public static bool TryCreate(
            EntityManager em,
            Entity battle,
            Entity actingPlayer,
            AttackingStageExecutionType type,
            Entity parent,
            Entity actingCharacter,
            out Entity context
        )
        {
            context = Entity.Null;

            if (
                type != AttackingStageExecutionType.NormalTurn
                && type != AttackingStageExecutionType.ExtraTurn
                && type != AttackingStageExecutionType.ExtraAction
            )
                return false;

            if (
                battle == Entity.Null
                || !em.Exists(battle)
                || !em.HasComponent<BattleTag>(battle)
                || !em.HasComponent<AttackingStageExecutionInstanceCounter>(battle)
                || !em.HasComponent<AttackingStageExecutionInstance>(battle)
                || actingPlayer == Entity.Null
                || !em.Exists(actingPlayer)
            )
                return false;

            if (type == AttackingStageExecutionType.NormalTurn)
            {
                if (parent != Entity.Null || actingCharacter != Entity.Null)
                    return false;
            }
            else
            {
                if (
                    parent == Entity.Null
                    || parent == battle
                    || !em.Exists(parent)
                    || !em.HasComponent<AttackingStageExecutionContext>(parent)
                    || em.GetComponentData<AttackingStageExecutionContext>(parent).Battle != battle
                )
                    return false;

                if (
                    type == AttackingStageExecutionType.ExtraAction
                    && (actingCharacter == Entity.Null || !em.Exists(actingCharacter))
                )
                    return false;
            }

            AttackingStageExecutionInstanceCounter counter =
                em.GetComponentData<AttackingStageExecutionInstanceCounter>(battle);

            // Zero is reserved as an invalid ID; no wrapping or silent reuse.
            if (counter.NextID == 0 || counter.NextID == uint.MaxValue)
                return false;

            context = em.CreateEntity();
            em.AddComponentData(
                context,
                new AttackingStageExecutionContext
                {
                    ContextID = counter.NextID,
                    Battle = battle,
                    ParentContext = parent,
                    ActingPlayer = actingPlayer,
                    ActingCharacter = actingCharacter,
                    ExecutionType = type,
                    ExecutionStatus = AttackingStageExecutionStatus.Created,
                    DeferredPhase = default,
                    RemainingActionPoints = 0,
                    NextActionIndex = 0,
                }
            );
            em.AddBuffer<PlannedAction>(context);

            counter.NextID++;
            em.SetComponentData(battle, counter);
            return true;
        }
    }
}
