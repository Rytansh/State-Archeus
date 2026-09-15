using System;
using System.Collections.Generic;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.Plans;
using Archeus.Battle.Presentation.Recipes;
using Archeus.Core.Debugging;

namespace Archeus.Battle.Presentation.Compiler
{
    public static class PresentationExecutionCompiler
    {
        public static PresentationExecutionPlan Compile(
            in PresentationActionPacket packet,
            PresentationFact[] facts,
            PresentationRecipe recipe
        )
        {
            if (packet.Status != PresentationActionPacketStatus.Sealed)
            {
                Logging.Warn(
                    LogCategory.Presentation,
                    $"[COMPILER] Action={packet.ActionExecutionID} "
                        + "cannot be compiled because its packet is not sealed."
                );

                return default;
            }

            if (recipe == null)
            {
                Logging.Warn(
                    LogCategory.Presentation,
                    $"[COMPILER] Action={packet.ActionExecutionID} " + "has no PresentationRecipe."
                );

                return default;
            }

            facts ??= Array.Empty<PresentationFact>();
            for (int i = 0; i < facts.Length; i++)
            {
                PresentationFact fact = facts[i];

                Logging.Info(
                    LogCategory.Presentation,
                    $"[COMPILER SOURCE] "
                        + $"Fact={i} | "
                        + $"Seq={fact.FactMetadata.Sequence} | "
                        + $"RuntimeResult={fact.FactMetadata.ActionResultIndex} | "
                        + $"Hit={fact.FactMetadata.HitIndex} | "
                        + $"Type={fact.FactType} | "
                        + $"Target={fact.FactMetadata.TargetRuntimeID} | "
                        + $"Damage={fact.FactPayload.HitPayload.Damage}"
                );
            }
            Dictionary<ushort, List<int>> factsByResult = BuildFactsByHitIndex(facts);
            SortedDictionary<int, List<PresentationFragmentPlan>> fragmentsByImpact = new();
            PresentationHitBinding[] resultBindings = recipe.ResultBindings;

            for (int bindingIndex = 0; bindingIndex < resultBindings.Length; bindingIndex++)
            {
                PresentationHitBinding resultBinding = resultBindings[bindingIndex];
                if (
                    !factsByResult.TryGetValue(
                        resultBinding.HitIndex,
                        out List<int> matchingFactIndices
                    )
                )
                {
                    continue;
                }

                PresentationImpactBinding[] impactBindings = resultBinding.Impacts;

                if (impactBindings == null || impactBindings.Length == 0)
                {
                    Logging.Warn(
                        LogCategory.Presentation,
                        $"[COMPILER] Result={resultBinding.HitIndex} " + "has no impact bindings."
                    );

                    continue;
                }

                for (
                    int matchingIndex = 0;
                    matchingIndex < matchingFactIndices.Count;
                    matchingIndex++
                )
                {
                    int sourceFactIndex = matchingFactIndices[matchingIndex];

                    PresentationFact sourceFact = facts[sourceFactIndex];

                    if (!TryGetDisplayValue(in sourceFact, out float totalDisplayValue))
                    {
                        Logging.Warn(
                            LogCategory.Presentation,
                            $"[COMPILER] Unsupported fact type "
                                + $"{sourceFact.FactType} | "
                                + $"Action={packet.ActionExecutionID} | "
                                + $"Result={resultBinding.HitIndex}"
                        );

                        continue;
                    }

                    float remainingValue = totalDisplayValue;

                    for (
                        int impactBindingIndex = 0;
                        impactBindingIndex < impactBindings.Length;
                        impactBindingIndex++
                    )
                    {
                        PresentationImpactBinding impactBinding = impactBindings[
                            impactBindingIndex
                        ];

                        float displayValue;

                        if (impactBindingIndex != impactBindings.Length - 1)
                        {
                            displayValue = totalDisplayValue * impactBinding.Weight;

                            remainingValue -= displayValue;
                        }
                        else
                        {
                            displayValue = remainingValue;
                        }

                        if (
                            !fragmentsByImpact.TryGetValue(
                                impactBinding.ImpactIndex,
                                out List<PresentationFragmentPlan> impactFragments
                            )
                        )
                        {
                            impactFragments = new List<PresentationFragmentPlan>();

                            fragmentsByImpact.Add(impactBinding.ImpactIndex, impactFragments);
                        }

                        impactFragments.Add(
                            new PresentationFragmentPlan
                            {
                                SourceFactIndex = sourceFactIndex,
                                DisplayValue = displayValue,
                            }
                        );
                    }
                }
            }

            FlattenImpactData(
                fragmentsByImpact,
                out PresentationImpactCue[] impactPlans,
                out PresentationFragmentPlan[] fragmentPlans
            );

            return new PresentationExecutionPlan(
                packet.BattleRuntimeID,
                packet.ActionExecutionID,
                packet.ActionDefinitionID,
                packet.SourceRuntimeID,
                packet.FirstSequence,
                packet.LastSequence,
                recipe,
                facts,
                impactPlans,
                fragmentPlans
            );
        }

        private static Dictionary<ushort, List<int>> BuildFactsByHitIndex(PresentationFact[] facts)
        {
            Dictionary<ushort, List<int>> factsByResult = new();

            for (int factIndex = 0; factIndex < facts.Length; factIndex++)
            {
                PresentationFact fact = facts[factIndex];
                ushort resultIndex = fact.FactMetadata.HitIndex;
                if (resultIndex == PresentationFactMetadata.NoActionResult)
                {
                    continue;
                }

                if (!factsByResult.TryGetValue(resultIndex, out List<int> resultFacts))
                {
                    resultFacts = new List<int>();

                    factsByResult.Add(resultIndex, resultFacts);
                }

                resultFacts.Add(factIndex);
            }

            return factsByResult;
        }

        private static bool TryGetDisplayValue(in PresentationFact fact, out float displayValue)
        {
            switch (fact.FactType)
            {
                case PresentationFactType.DamageApplied:
                {
                    displayValue = fact.FactPayload.HitPayload.Damage;

                    return true;
                }

                default:
                {
                    displayValue = 0f;
                    return false;
                }
            }
        }

        private static void FlattenImpactData(
            SortedDictionary<int, List<PresentationFragmentPlan>> fragmentsByImpact,
            out PresentationImpactCue[] impacts,
            out PresentationFragmentPlan[] fragments
        )
        {
            impacts = new PresentationImpactCue[fragmentsByImpact.Count];

            List<PresentationFragmentPlan> flattenedFragments = new();

            int impactPlanIndex = 0;

            foreach (KeyValuePair<int, List<PresentationFragmentPlan>> pair in fragmentsByImpact)
            {
                int fragmentStart = flattenedFragments.Count;

                List<PresentationFragmentPlan> impactFragments = pair.Value;

                flattenedFragments.AddRange(impactFragments);

                impacts[impactPlanIndex] = new PresentationImpactCue
                {
                    ImpactIndex = pair.Key,

                    FragmentStart = fragmentStart,

                    FragmentCount = impactFragments.Count,
                };

                impactPlanIndex++;
            }

            fragments = flattenedFragments.ToArray();
        }
    }
}
