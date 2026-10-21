using Archeus.Battle.Components.Core;
using Archeus.Game.Bootstrap;
using NUnit.Framework;

namespace Archeus.Testing.Battle.Randomness
{
    public class BattleRNGTests
    {
        private const ulong TestSeed = 12345678UL;

        // Creates the same initial state as BattleCreationSystem.
        private static BattleRNG CreateBattleRNG(ulong seed)
        {
            DeterministicRNG generator = new DeterministicRNG(seed);

            return new BattleRNG { StateA = generator.StateA, StateB = generator.StateB };
        }

        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            BattleRNG rngA = CreateBattleRNG(TestSeed);
            BattleRNG rngB = CreateBattleRNG(TestSeed);

            for (int i = 0; i < 100; i++)
            {
                int resultA = BattleRNGService.RollInt(ref rngA, 0, 100);
                int resultB = BattleRNGService.RollInt(ref rngB, 0, 100);

                Assert.That(resultA, Is.EqualTo(resultB));
            }

            Assert.That(rngA.StateA, Is.EqualTo(rngB.StateA));
            Assert.That(rngA.StateB, Is.EqualTo(rngB.StateB));
        }

        [Test]
        public void PerformingRoll_AdvancesRNGState()
        {
            BattleRNG rng = CreateBattleRNG(TestSeed);

            ulong previousA = rng.StateA;
            ulong previousB = rng.StateB;

            BattleRNGService.RollInt(ref rng, 0, 100);

            bool stateChanged = rng.StateA != previousA || rng.StateB != previousB;

            Assert.That(stateChanged, Is.True);
        }

        [Test]
        public void RestoringState_ReproducesRemainingSequence()
        {
            BattleRNG original = CreateBattleRNG(TestSeed);

            // Run several rolls before taking a snapshot.
            for (int i = 0; i < 10; i++)
            {
                BattleRNGService.RollInt(ref original, 0, 100);
            }

            // Save both RNG state fields.
            BattleRNG snapshot = original;

            // Continue from the snapshot point.
            int[] expectedResults = new int[20];

            for (int i = 0; i < expectedResults.Length; i++)
            {
                expectedResults[i] = BattleRNGService.RollInt(ref original, 0, 100);
            }

            // Restore the earlier state.
            BattleRNG restored = snapshot;

            // The continuation must match exactly.
            for (int i = 0; i < expectedResults.Length; i++)
            {
                int result = BattleRNGService.RollInt(ref restored, 0, 100);

                Assert.That(result, Is.EqualTo(expectedResults[i]));
            }

            Assert.That(restored.StateA, Is.EqualTo(original.StateA));
            Assert.That(restored.StateB, Is.EqualTo(original.StateB));
        }

        [Test]
        public void RollInt_AlwaysReturnsWithinRange()
        {
            BattleRNG rng = CreateBattleRNG(TestSeed);

            for (int i = 0; i < 100; i++)
            {
                int result = BattleRNGService.RollInt(ref rng, 10, 50);

                Assert.That(result, Is.GreaterThanOrEqualTo(10));
                Assert.That(result, Is.LessThan(50));
            }
        }

        [Test]
        public void RollChance_RespectsCertainAndImpossibleChances()
        {
            BattleRNG rng = CreateBattleRNG(TestSeed);

            Assert.That(BattleRNGService.RollChance(ref rng, 0f), Is.False);

            Assert.That(BattleRNGService.RollChance(ref rng, 100f), Is.True);
        }

        [Test]
        public void FixedSeed_ProducesKnownSequence()
        {
            BattleRNG rng = CreateBattleRNG(TestSeed);

            // Expected values from the current RNG algorithm.
            int[] expected = { 51, 48, 11, 84, 77, 1, 28, 52, 11, 13 };

            for (int i = 0; i < expected.Length; i++)
            {
                int result = BattleRNGService.RollInt(ref rng, 0, 100);

                Assert.That(result, Is.EqualTo(expected[i]), $"RNG sequence changed at roll {i}");
            }
        }
    }
}
