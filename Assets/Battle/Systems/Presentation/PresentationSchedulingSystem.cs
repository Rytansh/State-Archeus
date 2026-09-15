using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation.Plans;
using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup))]
    [UpdateAfter(typeof(PresentationPlanCompilerSystem))]
    public partial class PresentationSchedulingSystem : SystemBase
    {
        private Entity currentPlanEntity;

        protected override void OnCreate()
        {
            currentPlanEntity = Entity.Null;
        }

        protected override void OnUpdate()
        {
            if (currentPlanEntity != Entity.Null)
            {
                if (!EntityManager.Exists(currentPlanEntity))
                {
                    currentPlanEntity = Entity.Null;
                }
                else
                {
                    PresentationPlanPlaybackState state =
                        EntityManager.GetComponentData<PresentationPlanPlaybackState>(
                            currentPlanEntity
                        );

                    switch (state.Status)
                    {
                        case PresentationPlanStatus.Running:
                        {
                            return;
                        }

                        case PresentationPlanStatus.Completed:
                        {
                            FinishCurrentPlan(success: true);
                            break;
                        }

                        case PresentationPlanStatus.Failed:
                        {
                            FinishCurrentPlan(success: false);
                            break;
                        }
                    }
                }
            }

            Entity nextPlanEntity = FindNextReadyPlan();

            if (nextPlanEntity == Entity.Null)
            {
                return;
            }

            StartPlan(nextPlanEntity);
        }

        private Entity FindNextReadyPlan()
        {
            Entity bestEntity = Entity.Null;

            ulong bestSequence = ulong.MaxValue;

            foreach (
                var (state, entity) in SystemAPI
                    .Query<RefRO<PresentationPlanPlaybackState>>()
                    .WithEntityAccess()
            )
            {
                if (state.ValueRO.Status != PresentationPlanStatus.Ready)
                {
                    continue;
                }

                PresentationExecutionPlanReference planReference =
                    EntityManager.GetComponentObject<PresentationExecutionPlanReference>(entity);

                PresentationExecutionPlan plan = planReference.Plan;

                if (plan.FirstSequence >= bestSequence)
                {
                    continue;
                }

                bestSequence = plan.FirstSequence;

                bestEntity = entity;
            }

            return bestEntity;
        }

        private void StartPlan(Entity planEntity)
        {
            PresentationPlanPlaybackState state =
                EntityManager.GetComponentData<PresentationPlanPlaybackState>(planEntity);

            state.Status = PresentationPlanStatus.Running;

            EntityManager.SetComponentData(planEntity, state);

            currentPlanEntity = planEntity;

            PresentationExecutionPlanReference planReference =
                EntityManager.GetComponentObject<PresentationExecutionPlanReference>(planEntity);

            Logging.Info(
                LogCategory.Presentation,
                $"[SCHEDULER] Started plan | "
                    + $"Action={planReference.Plan.ActionExecutionID} | "
                    + $"Sequence={planReference.Plan.FirstSequence}"
                    + $"..{planReference.Plan.LastSequence}"
            );
        }

        private void FinishCurrentPlan(bool success)
        {
            PresentationExecutionPlanReference planReference =
                EntityManager.GetComponentObject<PresentationExecutionPlanReference>(
                    currentPlanEntity
                );

            Logging.Info(
                LogCategory.Presentation,
                $"[SCHEDULER] "
                    + $"{(success ? "Completed" : "Failed")} plan | "
                    + $"Action={planReference.Plan.ActionExecutionID}"
            );

            EntityManager.DestroyEntity(currentPlanEntity);
            currentPlanEntity = Entity.Null;
        }
    }
}
