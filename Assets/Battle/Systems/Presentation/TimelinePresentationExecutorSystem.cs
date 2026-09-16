using System.Collections.Generic;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation;
using Archeus.Battle.Presentation.Plans;
using Archeus.Battle.Presentation.Presenters;
using Archeus.Battle.Presentation.Recipes;
using Archeus.Core.Debugging;
using Unity.Entities;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup))]
    [UpdateAfter(typeof(PresentationSchedulingSystem))]
    public partial class TimelinePresentationExecutorSystem : SystemBase
    {
        private EntityQuery runnerQuery;

        private EntityQuery presentationRegistryQuery;

        private BattlePresentationRunner runner;

        private DamageFactPresenter damageFactPresenter;

        private PresentationRegistry presentationRegistry;

        private Entity activePlanEntity;

        private bool playbackStopped;

        private readonly Queue<int> pendingImpactIndices = new();

        private readonly HashSet<int> consumedImpactIndices = new();

        protected override void OnCreate()
        {
            runnerQuery = GetEntityQuery(
                ComponentType.ReadOnly<BattlePresentationRunnerReference>()
            );

            presentationRegistryQuery = GetEntityQuery(
                ComponentType.ReadOnly<PresentationRegistryReference>()
            );

            RequireForUpdate(runnerQuery);

            RequireForUpdate(presentationRegistryQuery);

            activePlanEntity = Entity.Null;
        }

        protected override void OnDestroy()
        {
            UnsubscribeFromRunner();
        }

        protected override void OnUpdate()
        {
            EnsureRunner();

            EnsurePresentationRegistry();

            if (runner == null || presentationRegistry == null)
            {
                return;
            }

            ProcessPendingImpacts();

            if (playbackStopped)
            {
                CompleteActivePlan();

                playbackStopped = false;
            }

            if (activePlanEntity != Entity.Null)
            {
                if (!EntityManager.Exists(activePlanEntity))
                {
                    activePlanEntity = Entity.Null;
                }
                else
                {
                    return;
                }
            }

            Entity nextPlanEntity = FindRunningTimelinePlan();

            if (nextPlanEntity == Entity.Null)
                return;

            StartTimelinePlan(nextPlanEntity);
        }

        private void OnImpactRequested(int impactIndex)
        {
            pendingImpactIndices.Enqueue(impactIndex);
        }

        private void EnsureRunner()
        {
            Entity runnerEntity = runnerQuery.GetSingletonEntity();

            BattlePresentationRunnerReference reference =
                EntityManager.GetComponentObject<BattlePresentationRunnerReference>(runnerEntity);

            BattlePresentationRunner resolvedRunner = reference.Runner;

            if (resolvedRunner == runner)
                return;

            UnsubscribeFromRunner();

            runner = resolvedRunner;

            if (runner != null)
            {
                runner.PlaybackStopped += OnPlaybackStopped;
                runner.ImpactRequested += OnImpactRequested;

                if (runner.DamageNumberPresenter != null)
                {
                    damageFactPresenter = new DamageFactPresenter(runner.DamageNumberPresenter);
                }
            }
        }

        private void EnsurePresentationRegistry()
        {
            Entity registryEntity = presentationRegistryQuery.GetSingletonEntity();

            PresentationRegistryReference reference =
                EntityManager.GetComponentObject<PresentationRegistryReference>(registryEntity);

            presentationRegistry = reference.Registry;
        }

        private void UnsubscribeFromRunner()
        {
            if (runner == null)
                return;

            runner.PlaybackStopped -= OnPlaybackStopped;
            runner.ImpactRequested -= OnImpactRequested;

            runner = null;
            damageFactPresenter = null;
        }

        private Entity FindRunningTimelinePlan()
        {
            Entity bestEntity = Entity.Null;

            ulong bestSequence = ulong.MaxValue;

            foreach (
                var (playbackState, entity) in SystemAPI
                    .Query<RefRO<PresentationPlanPlaybackState>>()
                    .WithEntityAccess()
            )
            {
                if (playbackState.ValueRO.Status != PresentationPlanStatus.Running)
                {
                    continue;
                }

                PresentationExecutionPlanReference planReference =
                    EntityManager.GetComponentObject<PresentationExecutionPlanReference>(entity);

                PresentationExecutionPlan plan = planReference.Plan;

                if (plan.Backend != PresentationBackendKind.Timeline)
                {
                    continue;
                }

                if (plan.FirstSequence >= bestSequence)
                {
                    continue;
                }

                bestSequence = plan.FirstSequence;

                bestEntity = entity;
            }

            return bestEntity;
        }

        private void ProcessPendingImpacts()
        {
            while (pendingImpactIndices.Count > 0)
            {
                int impactIndex = pendingImpactIndices.Dequeue();

                ConsumeImpact(impactIndex, false);
            }
        }

        private void ConsumeImpact(int impactIndex, bool isFallback)
        {
            if (activePlanEntity == Entity.Null)
            {
                Logging.Warn(
                    LogCategory.Presentation,
                    $"[IMPACT] Received Impact=" + $"{impactIndex} " + "without an active plan."
                );

                return;
            }

            if (!EntityManager.Exists(activePlanEntity))
            {
                return;
            }

            /*
             * Prevent duplicate Timeline markers from
             * consuming the same impact twice.
             */
            if (!consumedImpactIndices.Add(impactIndex))
            {
                Logging.Warn(
                    LogCategory.Presentation,
                    $"[IMPACT] Impact={impactIndex} " + "was already consumed."
                );

                return;
            }

            PresentationExecutionPlanReference planReference =
                EntityManager.GetComponentObject<PresentationExecutionPlanReference>(
                    activePlanEntity
                );

            PresentationExecutionPlan plan = planReference.Plan;

            PresentationImpactCue? matchingCue = null;

            for (int i = 0; i < plan.Impacts.Length; i++)
            {
                if (plan.Impacts[i].ImpactIndex != impactIndex)
                {
                    continue;
                }

                matchingCue = plan.Impacts[i];

                break;
            }

            /*
             * This is allowed.
             *
             * An authored marker may exist even when this
             * particular execution produced no facts for it.
             */
            if (!matchingCue.HasValue)
            {
                Logging.Info(
                    LogCategory.Presentation,
                    $"[IMPACT] No-op | "
                        + $"Action="
                        + $"{plan.ActionExecutionID} | "
                        + $"Impact={impactIndex}"
                );

                return;
            }

            PresentationImpactCue cue = matchingCue.Value;

            Logging.Info(
                LogCategory.Presentation,
                $"[IMPACT] Consuming | "
                    + $"Action="
                    + $"{plan.ActionExecutionID} | "
                    + $"Impact={impactIndex} | "
                    + $"Fragments="
                    + $"{cue.FragmentCount} | "
                    + $"Fallback={isFallback}"
            );

            int end = cue.FragmentStart + cue.FragmentCount;

            for (int fragmentIndex = cue.FragmentStart; fragmentIndex < end; fragmentIndex++)
            {
                PresentationFragmentPlan fragment = plan.Fragments[fragmentIndex];

                PresentationFact fact = plan.SourceFacts[fragment.SourceFactIndex];

                uint targetRuntimeID = fact.FactMetadata.TargetRuntimeID;

                if (
                    !presentationRegistry.TryGet(
                        targetRuntimeID,
                        out CharacterPresenter targetPresenter
                    )
                )
                {
                    Logging.Warn(
                        LogCategory.Presentation,
                        $"[IMPACT] Missing target presenter | " + $"RuntimeID={targetRuntimeID}"
                    );

                    continue;
                }

                if (damageFactPresenter == null)
                {
                    Logging.Warn(
                        LogCategory.Presentation,
                        "[IMPACT] DamageFactPresenter is unavailable."
                    );

                    continue;
                }

                damageFactPresenter.Present(in fact, fragment.DisplayValue, targetPresenter);

                Logging.Info(
                    LogCategory.Presentation,
                    $"[IMPACT FRAGMENT] "
                        + $"Impact="
                        + $"{impactIndex} | "
                        + $"Target="
                        + $"{targetRuntimeID} | "
                        + $"Value="
                        + $"{fragment.DisplayValue} | "
                        + $"Type="
                        + $"{fact.FactType}"
                );
            }
        }

        private void StartTimelinePlan(Entity planEntity)
        {
            PresentationExecutionPlanReference planReference =
                EntityManager.GetComponentObject<PresentationExecutionPlanReference>(planEntity);

            PresentationExecutionPlan plan = planReference.Plan;

            if (plan.Timeline == null)
            {
                Logging.Warn(
                    LogCategory.Presentation,
                    $"[TIMELINE] Action=" + $"{plan.ActionExecutionID} " + "has no Timeline."
                );

                FailPlan(planEntity);

                return;
            }

            /*
             * Stage 1:
             * Give the runner the Timeline asset WITHOUT
             * starting playback yet.
             */
            if (!runner.TryPrepare(plan))
            {
                /*
                 * The reusable director may currently be
                 * occupied.
                 *
                 * Retry on another update.
                 */
                return;
            }

            /*
             * Stage 2:
             * Resolve semantic Timeline roles into actual
             * runtime presentation objects.
             */
            if (!TryBindTimelineRoles(plan, runner.PrimaryDirector))
            {
                ClearTimelineBindings(plan, runner.PrimaryDirector);

                runner.CancelPrepared();

                FailPlan(planEntity);

                return;
            }

            activePlanEntity = planEntity;
            consumedImpactIndices.Clear();
            pendingImpactIndices.Clear();

            /*
             * Stage 3:
             * Only now is the Timeline safe to play.
             */
            if (!runner.PlayPrepared())
            {
                ClearTimelineBindings(plan, runner.PrimaryDirector);

                runner.CancelPrepared();

                FailPlan(planEntity);

                return;
            }

            Logging.Info(
                LogCategory.Presentation,
                $"[TIMELINE] Started | "
                    + $"Action={plan.ActionExecutionID} | "
                    + $"Source={plan.SourceRuntimeID} | "
                    + $"Timeline={plan.Timeline.name}"
            );
        }

        private bool TryBindTimelineRoles(PresentationExecutionPlan plan, PlayableDirector director)
        {
            if (plan.Timeline == null)
                return false;

            foreach (TrackAsset track in plan.Timeline.GetOutputTracks())
            {
                if (track is not IPresentationRoleTrack roleTrack)
                {
                    continue;
                }

                switch (roleTrack.Role)
                {
                    case PresentationTrackRole.None:
                        continue;

                    case PresentationTrackRole.SourceAnimator:
                    {
                        if (
                            !presentationRegistry.TryGet(
                                plan.SourceRuntimeID,
                                out CharacterPresenter presenter
                            )
                        )
                        {
                            Logging.Warn(
                                LogCategory.Presentation,
                                $"[TIMELINE] Missing source presenter | "
                                    + $"RuntimeID={plan.SourceRuntimeID}"
                            );

                            return false;
                        }

                        if (presenter.Animator == null)
                        {
                            Logging.Warn(
                                LogCategory.Presentation,
                                $"[TIMELINE] Source presenter has no Animator | "
                                    + $"RuntimeID={plan.SourceRuntimeID}"
                            );

                            return false;
                        }

                        director.SetGenericBinding(track, presenter.Animator);

                        Logging.Info(
                            LogCategory.Presentation,
                            $"[TIMELINE] Bound SourceAnimator | "
                                + $"Track={track.name} | "
                                + $"RuntimeID={plan.SourceRuntimeID} | "
                                + $"Presenter={presenter.name}"
                        );

                        break;
                    }

                    default:
                    {
                        Logging.Warn(
                            LogCategory.Presentation,
                            $"[TIMELINE] Unsupported role "
                                + $"{roleTrack.Role} | "
                                + $"Track={track.name}"
                        );

                        return false;
                    }
                }
            }

            return true;
        }

        private void ClearTimelineBindings(
            PresentationExecutionPlan plan,
            PlayableDirector director
        )
        {
            if (plan?.Timeline == null)
                return;

            foreach (TrackAsset track in plan.Timeline.GetOutputTracks())
            {
                if (track is not IPresentationRoleTrack)
                {
                    continue;
                }

                director.ClearGenericBinding(track);
            }
        }

        private void OnPlaybackStopped(PresentationExecutionPlan plan)
        {
            /*
             * Keep Unity callback side-effects tiny.
             *
             * ECS state changes happen during OnUpdate.
             */
            playbackStopped = true;
        }

        private void CompleteActivePlan()
        {
            if (activePlanEntity == Entity.Null)
            {
                return;
            }

            if (!EntityManager.Exists(activePlanEntity))
            {
                activePlanEntity = Entity.Null;

                return;
            }

            PresentationExecutionPlanReference planReference =
                EntityManager.GetComponentObject<PresentationExecutionPlanReference>(
                    activePlanEntity
                );

            PresentationExecutionPlan plan = planReference.Plan;

            FallbackConsumeUnconsumedImpacts(plan);

            /*
             * The reusable director must not retain
             * bindings to the previous character.
             */
            ClearTimelineBindings(plan, runner.PrimaryDirector);

            runner.ResetDirector();

            PresentationPlanPlaybackState state =
                EntityManager.GetComponentData<PresentationPlanPlaybackState>(activePlanEntity);

            state.Status = PresentationPlanStatus.Completed;

            EntityManager.SetComponentData(activePlanEntity, state);

            Logging.Info(
                LogCategory.Presentation,
                $"[TIMELINE] Completed | " + $"Action=" + $"{plan.ActionExecutionID}"
            );

            activePlanEntity = Entity.Null;
        }

        private void FallbackConsumeUnconsumedImpacts(PresentationExecutionPlan plan)
        {
            for (int i = 0; i < plan.Impacts.Length; i++)
            {
                int impactIndex = plan.Impacts[i].ImpactIndex;

                if (consumedImpactIndices.Contains(impactIndex))
                {
                    continue;
                }

                Logging.Warn(
                    LogCategory.Presentation,
                    $"[TIMELINE] Missing marker for "
                        + $"Impact={impactIndex}. "
                        + "Fallback-consuming."
                );

                ConsumeImpact(impactIndex, true);
            }
        }

        private void FailPlan(Entity planEntity)
        {
            PresentationPlanPlaybackState state =
                EntityManager.GetComponentData<PresentationPlanPlaybackState>(planEntity);

            state.Status = PresentationPlanStatus.Failed;

            EntityManager.SetComponentData(planEntity, state);
        }
    }
}
