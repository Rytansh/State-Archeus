using System;
using Archeus.Battle.Presentation.Recipes;
using UnityEngine;
using UnityEngine.Timeline;

namespace Archeus.Battle.Presentation
{
    public interface IPresentationRoleTrack
    {
        PresentationTrackRole Role { get; }
    }

    [Serializable]
    public sealed class PresentationAnimationTrack : AnimationTrack, IPresentationRoleTrack
    {
        [SerializeField]
        private PresentationTrackRole role;

        public PresentationTrackRole Role => role;
    }
}
