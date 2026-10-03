using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace Archeus.Battle.Presentation.Recipes
{
    public enum PresentationBackendKind : byte
    {
        Generic = 0,
        Timeline = 1,
    }

    [CreateAssetMenu(
        fileName = "PresentationRecipe",
        menuName = "Archeus/Presentation/Presentation Recipe"
    )]
    public sealed class PresentationRecipe : ScriptableObject
    {
        private const float WeightTolerance = 0.0001f;

        [SerializeField]
        private PresentationBackendKind backend = PresentationBackendKind.Timeline;

        [SerializeField]
        private TimelineAsset timeline;

        [SerializeField]
        private PresentationHitBinding[] resultBindings = Array.Empty<PresentationHitBinding>();

        public PresentationBackendKind Backend => backend;

        public TimelineAsset Timeline => timeline;

        public PresentationHitBinding[] ResultBindings => resultBindings;

        public bool TryValidate(out string error)
        {
            if (backend == PresentationBackendKind.Timeline && timeline == null)
            {
                error =
                    $"Presentation recipe '{name}' uses the Timeline backend "
                    + "but has no Timeline assigned.";

                return false;
            }

            HashSet<ushort> seenResultIndices = new();

            for (int resultIndex = 0; resultIndex < resultBindings.Length; resultIndex++)
            {
                PresentationHitBinding resultBinding = resultBindings[resultIndex];

                if (resultBinding == null)
                {
                    error =
                        $"Presentation recipe '{name}' contains a null "
                        + $"hit binding at index {resultIndex}.";

                    return false;
                }

                if (!seenResultIndices.Add(resultBinding.HitIndex))
                {
                    error =
                        $"Presentation recipe '{name}' contains more than one "
                        + $"binding for HitIndex={resultBinding.HitIndex}.";

                    return false;
                }

                PresentationImpactBinding[] impacts = resultBinding.Impacts;

                if (impacts == null || impacts.Length == 0)
                {
                    error =
                        $"Presentation recipe '{name}' has no impacts for "
                        + $"ResultIndex={resultBinding.HitIndex}.";

                    return false;
                }

                HashSet<int> seenImpactIndices = new();

                float totalWeight = 0f;

                for (int impactIndex = 0; impactIndex < impacts.Length; impactIndex++)
                {
                    PresentationImpactBinding impact = impacts[impactIndex];

                    if (impact.ImpactIndex < 0)
                    {
                        error =
                            $"Presentation recipe '{name}' contains a negative "
                            + $"ImpactIndex for ResultIndex={resultBinding.HitIndex}.";

                        return false;
                    }

                    if (!seenImpactIndices.Add(impact.ImpactIndex))
                    {
                        error =
                            $"Presentation recipe '{name}' maps "
                            + $"ResultIndex={resultBinding.HitIndex} to "
                            + $"ImpactIndex={impact.ImpactIndex} more than once.";

                        return false;
                    }

                    if (
                        float.IsNaN(impact.Weight)
                        || float.IsInfinity(impact.Weight)
                        || impact.Weight <= 0f
                    )
                    {
                        error =
                            $"Presentation recipe '{name}' contains an invalid "
                            + $"weight for ResultIndex={resultBinding.HitIndex}, "
                            + $"ImpactIndex={impact.ImpactIndex}.";

                        return false;
                    }

                    totalWeight += impact.Weight;
                }

                if (Mathf.Abs(totalWeight - 1f) > WeightTolerance)
                {
                    error =
                        $"Presentation recipe '{name}' weights for "
                        + $"ResultIndex={resultBinding.HitIndex} total "
                        + $"{totalWeight}, but must total 1.";

                    return false;
                }
            }

            error = null;
            return true;
        }

        private bool TimelineContainsTrack(TrackAsset track)
        {
            if (timeline == null)
                return false;

            foreach (TrackAsset outputTrack in timeline.GetOutputTracks())
            {
                if (outputTrack == track)
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!TryValidate(out string error))
            {
                Debug.LogError(error, this);
            }
        }
#endif
    }

    [Serializable]
    public sealed class PresentationHitBinding
    {
        [SerializeField]
        private ushort hitIndex;

        [SerializeField]
        private PresentationImpactBinding[] impacts = Array.Empty<PresentationImpactBinding>();

        public ushort HitIndex => hitIndex;
        public PresentationImpactBinding[] Impacts => impacts;
    }

    [Serializable]
    public struct PresentationImpactBinding
    {
        [SerializeField]
        private int impactIndex;

        [SerializeField]
        private float weight;

        public int ImpactIndex => impactIndex;
        public float Weight => weight;
    }
}
