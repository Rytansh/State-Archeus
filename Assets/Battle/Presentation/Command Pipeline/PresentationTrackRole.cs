using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace Archeus.Battle.Presentation.Recipes
{
    public enum PresentationTrackRole : byte
    {
        None = 0,
        SourceAnimator = 1,

        // Later:
        BattleCamera = 2,
        BattlefieldRoot = 3,
    }
}
