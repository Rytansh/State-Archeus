using Archeus.Game.Bootstrap;
using NUnit.Framework;

namespace Archeus.Testing.Battle.Snapshots
{
    public sealed class DeterministicRngSnapshotTests
    {
        [Test]
        public void MidstreamSnapshotReproducesMixedRandomOperations()
        {
            var rng = new DeterministicRNG(12345678);
            for (int i = 0; i < 75; i++)
                rng.NextInt(0, 1000);
            ulong stateA = rng.StateA;
            ulong stateB = rng.StateB;
            var restored = new DeterministicRNG(stateA, stateB);
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(restored.NextInt(0, 1000), Is.EqualTo(rng.NextInt(0, 1000)));
                Assert.That(restored.NextBool(), Is.EqualTo(rng.NextBool()));
                Assert.That(restored.NextFloat(), Is.EqualTo(rng.NextFloat()));
                Assert.That(restored.NextDouble(), Is.EqualTo(rng.NextDouble()));
            }
            Assert.That(restored.StateA, Is.EqualTo(rng.StateA));
            Assert.That(restored.StateB, Is.EqualTo(rng.StateB));
        }

        [Test]
        public void SeedIsNotASubstituteForCurrentState()
        {
            var baseline = new DeterministicRNG(12345678);
            for (int i = 0; i < 25; i++)
                baseline.NextInt(0, 1000);
            var reseeded = new DeterministicRNG(12345678);
            Assert.That(
                (baseline.StateA, baseline.StateB),
                Is.Not.EqualTo((reseeded.StateA, reseeded.StateB))
            );
        }

        [Test]
        public void ManyRestoreReplayCyclesReturnTheSameContinuation()
        {
            var baseline = new DeterministicRNG(987654321);
            baseline.NextInt(0, 1000);
            var expected = baseline;
            int value = expected.NextInt(0, 1000);
            for (int cycle = 0; cycle < 100; cycle++)
            {
                var restored = new DeterministicRNG(baseline.StateA, baseline.StateB);
                Assert.That(restored.NextInt(0, 1000), Is.EqualTo(value));
                Assert.That(restored.StateA, Is.EqualTo(expected.StateA));
                Assert.That(restored.StateB, Is.EqualTo(expected.StateB));
            }
        }
    }
}
