using System.Collections.Generic;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation;
using Archeus.Battle.Presentation.Compiler;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.Plans;
using Archeus.Battle.Presentation.Recipes;
using Archeus.Core.Debugging;
using Unity.Entities;
using UnityEngine;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup))]
    [UpdateAfter(typeof(BattlePresentationAssemblySystem))]
    public partial class PresentationPlanCompilerSystem : SystemBase
    {
        private EntityQuery recipeRegistryQuery;

        private bool stubRecipeRegistered;
        private bool warnedAboutMissingStubRecipe;

        private readonly List<PendingCompiledPlan> pendingPlans = new();

        protected override void OnCreate()
        {
            recipeRegistryQuery = GetEntityQuery(
                ComponentType.ReadOnly<PresentationRecipeRegistryReference>()
            );

            RequireForUpdate(recipeRegistryQuery);
        }

        protected override void OnUpdate()
        {
            Entity registryEntity = recipeRegistryQuery.GetSingletonEntity();

            PresentationRecipeRegistryReference registryReference =
                EntityManager.GetComponentObject<PresentationRecipeRegistryReference>(
                    registryEntity
                );

            PresentationRecipeRegistry recipeRegistry = registryReference.Registry;

            TryRegisterStubRecipe(recipeRegistry);

            pendingPlans.Clear();

            foreach (
                var (packet, actions, packetEntity) in SystemAPI
                    .Query<RefRO<PresentationActionPacket>, DynamicBuffer<PresentationAction>>()
                    .WithNone<PresentationPacketCompiledTag>()
                    .WithEntityAccess()
            )
            {
                PresentationActionPacket packetData = packet.ValueRO;

                if (packetData.Status != PresentationActionPacketStatus.Sealed)
                {
                    continue;
                }

                if (
                    !recipeRegistry.TryGet(
                        packetData.ActionDefinitionID,
                        out PresentationRecipe recipe
                    )
                )
                {
                    Logging.Warn(
                        LogCategory.Presentation,
                        $"[PLAN COMPILER] No recipe registered for "
                            + $"ActionDefinitionID={packetData.ActionDefinitionID} | "
                            + $"Action={packetData.ActionExecutionID}"
                    );

                    continue;
                }

                PresentationFact[] facts = CopyFacts(actions);

                PresentationExecutionPlan plan = PresentationExecutionCompiler.Compile(
                    in packetData,
                    facts,
                    recipe
                );

                if (plan == null)
                {
                    Logging.Warn(
                        LogCategory.Presentation,
                        $"[PLAN COMPILER] Failed to compile "
                            + $"Action={packetData.ActionExecutionID}"
                    );

                    continue;
                }

                pendingPlans.Add(new PendingCompiledPlan(packetEntity, plan));
            }

            PublishCompiledPlans();
        }

        private void TryRegisterStubRecipe(PresentationRecipeRegistry registry)
        {
            if (stubRecipeRegistered)
                return;

            if (registry.TryGet(0, out _))
            {
                stubRecipeRegistered = true;
                return;
            }

            PresentationTemporaryRecipeHolder provider =
                Object.FindFirstObjectByType<PresentationTemporaryRecipeHolder>();

            if (provider == null || provider.Recipe == null)
            {
                if (!warnedAboutMissingStubRecipe)
                {
                    Logging.Warn(
                        LogCategory.Presentation,
                        "[PLAN COMPILER] Could not find a "
                            + "PresentationRecipeStubProvider with a recipe. "
                            + "Waiting before registering ActionDefinitionID 0."
                    );

                    warnedAboutMissingStubRecipe = true;
                }

                return;
            }

            bool registered = registry.Register(0, provider.Recipe);

            if (!registered)
            {
                Logging.Warn(
                    LogCategory.Presentation,
                    "[PLAN COMPILER] Failed to register temporary "
                        + "PresentationRecipe under ActionDefinitionID 0."
                );

                return;
            }

            stubRecipeRegistered = true;

            Logging.Info(
                LogCategory.Presentation,
                $"[PLAN COMPILER] Registered temporary recipe "
                    + $"'{provider.Recipe.name}' "
                    + "for ActionDefinitionID=0."
            );
        }

        private static PresentationFact[] CopyFacts(DynamicBuffer<PresentationAction> actions)
        {
            PresentationFact[] facts = new PresentationFact[actions.Length];

            for (int i = 0; i < actions.Length; i++)
            {
                facts[i] = actions[i].Fact;
            }

            return facts;
        }

        private void PublishCompiledPlans()
        {
            for (int i = 0; i < pendingPlans.Count; i++)
            {
                PendingCompiledPlan pending = pendingPlans[i];

                Entity planEntity = EntityManager.CreateEntity();

                EntityManager.SetName(
                    planEntity,
                    $"Presentation Plan " + $"{pending.Plan.ActionExecutionID}"
                );

                EntityManager.AddComponentObject(
                    planEntity,
                    new PresentationExecutionPlanReference { Plan = pending.Plan }
                );

                EntityManager.AddComponentData(
                    planEntity,
                    new PresentationPlanPlaybackState { Status = PresentationPlanStatus.Ready }
                );
                EntityManager.AddComponent<PresentationPacketCompiledTag>(pending.PacketEntity);

                Logging.Info(
                    LogCategory.Presentation,
                    $"[PLAN COMPILER] Compiled | "
                        + $"Battle={pending.Plan.BattleRuntimeID} | "
                        + $"Action={pending.Plan.ActionExecutionID} | "
                        + $"Sequence={pending.Plan.FirstSequence}"
                        + $"..{pending.Plan.LastSequence} | "
                        + $"Impacts={pending.Plan.Impacts.Length} | "
                        + $"Fragments={pending.Plan.Fragments.Length}"
                );
                LogCompiledPlan(pending.Plan);
            }
        }

        private static void LogCompiledPlan(PresentationExecutionPlan plan)
        {
            for (int impactIndex = 0; impactIndex < plan.Impacts.Length; impactIndex++)
            {
                PresentationImpactCue impact = plan.Impacts[impactIndex];

                Logging.Info(
                    LogCategory.Presentation,
                    $"[PLAN] Impact={impact.ImpactIndex} | "
                        + $"FragmentStart={impact.FragmentStart} | "
                        + $"FragmentCount={impact.FragmentCount}"
                );

                int fragmentEnd = impact.FragmentStart + impact.FragmentCount;

                for (
                    int fragmentIndex = impact.FragmentStart;
                    fragmentIndex < fragmentEnd;
                    fragmentIndex++
                )
                {
                    PresentationFragmentPlan fragment = plan.Fragments[fragmentIndex];

                    if (
                        fragment.SourceFactIndex < 0
                        || fragment.SourceFactIndex >= plan.SourceFacts.Length
                    )
                    {
                        Logging.Warn(
                            LogCategory.Presentation,
                            $"[PLAN] Invalid SourceFactIndex=" + $"{fragment.SourceFactIndex}"
                        );

                        continue;
                    }

                    PresentationFact sourceFact = plan.SourceFacts[fragment.SourceFactIndex];

                    PresentationFactMetadata metadata = sourceFact.FactMetadata;

                    Logging.Info(
                        LogCategory.Presentation,
                        $"[PLAN]   Fragment={fragmentIndex} | "
                            + $"Fact={fragment.SourceFactIndex} | "
                            + $"RuntimeResult={metadata.ActionResultIndex} | "
                            + $"Hit={metadata.HitIndex} | "
                            + $"Target={metadata.TargetRuntimeID} | "
                            + $"Type={sourceFact.FactType} | "
                            + $"DisplayValue={fragment.DisplayValue}"
                    );
                }
            }
        }

        private readonly struct PendingCompiledPlan
        {
            public readonly Entity PacketEntity;
            public readonly PresentationExecutionPlan Plan;

            public PendingCompiledPlan(Entity packetEntity, PresentationExecutionPlan plan)
            {
                PacketEntity = packetEntity;
                Plan = plan;
            }
        }
    }
}
