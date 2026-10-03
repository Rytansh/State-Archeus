using System;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Presentation.Recipes;
using UnityEngine.Timeline;

namespace Archeus.Battle.Presentation.Plans
{
    public sealed class PresentationExecutionPlan
    {
        public ulong BattleRuntimeID;
        public uint ActionExecutionID;
        public uint ActionDefinitionID;
        public uint SourceRuntimeID;

        // Used by the scheduler to preserve presentation order.
        public ulong FirstSequence;
        public ulong LastSequence;

        public PresentationRecipe SourceRecipe;
        public PresentationBackendKind Backend;
        public TimelineAsset Timeline;

        public PresentationFact[] SourceFacts;
        public PresentationImpactCue[] Impacts;
        public PresentationFragmentPlan[] Fragments;

        public PresentationExecutionPlan(
            ulong battleRuntimeID,
            uint actionExecutionID,
            uint actionDefinitionID,
            uint sourceRuntimeID,
            ulong firstSequence,
            ulong lastSequence,
            PresentationRecipe sourceRecipe,
            PresentationFact[] sourceFacts,
            PresentationImpactCue[] impacts,
            PresentationFragmentPlan[] fragments
        )
        {
            SourceRecipe = sourceRecipe ?? throw new ArgumentNullException(nameof(sourceRecipe));

            BattleRuntimeID = battleRuntimeID;
            ActionExecutionID = actionExecutionID;
            ActionDefinitionID = actionDefinitionID;
            SourceRuntimeID = sourceRuntimeID;

            FirstSequence = firstSequence;
            LastSequence = lastSequence;

            Backend = sourceRecipe.Backend;
            Timeline = sourceRecipe.Timeline;

            SourceFacts = sourceFacts ?? Array.Empty<PresentationFact>();

            Impacts = impacts ?? Array.Empty<PresentationImpactCue>();

            Fragments = fragments ?? Array.Empty<PresentationFragmentPlan>();
        }
    }

    public struct PresentationImpactCue
    {
        public int ImpactIndex;

        public int FragmentStart;
        public int FragmentCount;
    }

    public struct PresentationFragmentPlan
    {
        public int SourceFactIndex;
        public float DisplayValue;
    }
}
