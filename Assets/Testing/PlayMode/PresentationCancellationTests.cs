using System.Collections;
using Archeus.Battle.Presentation;
using Archeus.Battle.Presentation.Plans;
using Archeus.Battle.Presentation.Presenters;
using Archeus.Battle.Presentation.Recipes;
using Archeus.Battle.Presentation.Timeline;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace Archeus.Testing.Battle.Snapshots
{
    public sealed class PresentationCancellationTests
    {
        [UnityTest]
        public IEnumerator CancellationStopsPlaybackAndSuppressesCompletionAndLateImpacts()
        {
            var root = new GameObject("Snapshot cancellation test");
            var runner = root.AddComponent<BattlePresentationRunner>();
            var recipe = ScriptableObject.CreateInstance<PresentationRecipe>();
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var marker = ScriptableObject.CreateInstance<PresentationConsumptionMarker>();
            try
            {
                timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
                timeline.fixedDuration = 5;
                var plan = new PresentationExecutionPlan(
                    42,
                    1,
                    0,
                    100,
                    1,
                    1,
                    recipe,
                    null,
                    null,
                    null
                )
                {
                    Timeline = timeline,
                };
                int completions = 0;
                int impacts = 0;
                runner.PlaybackStopped += _ => completions++;
                runner.ImpactRequested += _ => impacts++;
                Assert.That(runner.TryPrepare(plan), Is.True);
                Assert.That(runner.PlayPrepared(), Is.True);
                runner.CancelPlayback();
                runner.OnNotify(Playable.Null, marker, null);
                yield return null;
                Assert.That(runner.ActivePlan, Is.Null);
                Assert.That(runner.PrimaryDirector.playableAsset, Is.Null);
                Assert.That(completions, Is.Zero);
                Assert.That(impacts, Is.Zero);
                // Repeated invalidation is safe and does not synthesize a completion event.
                runner.CancelPlayback();
                Assert.That(completions, Is.Zero);
                Assert.That(runner.TryPrepare(plan), Is.True);
                var obsoleteGraph = PlayableGraph.Create("Abandoned notification graph");
                try
                {
                    runner.OnNotify(Playable.Create(obsoleteGraph), marker, null);
                    Assert.That(impacts, Is.Zero);
                }
                finally
                {
                    obsoleteGraph.Destroy();
                    runner.CancelPlayback();
                }
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(recipe);
                Object.Destroy(timeline);
                Object.Destroy(marker);
            }
        }

        [UnityTest]
        public IEnumerator DamageNumbersFromAbandonedActionsAreRemoved()
        {
            var root = new GameObject("Temporary damage numbers");
            try
            {
                var presenter = root.AddComponent<DamageNumberPresenter>();
                var number = new GameObject("Abandoned number");
                number.SetActive(false);
                number.transform.SetParent(root.transform);
                number.AddComponent<DamageNumberView>();
                presenter.Clear();
                yield return null;
                Assert.That(root.GetComponentsInChildren<DamageNumberView>(true), Is.Empty);
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
