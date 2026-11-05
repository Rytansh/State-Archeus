using Archeus.Battle.Snapshots;
using NUnit.Framework;

namespace Archeus.Testing.Battle.Snapshots
{
    public sealed class StageSnapshotWindowTests
    {
        [Test]
        public void NewWindowIsInvalid()
        {
            var window = new StageSnapshotWindow();
            Assert.That(window.CanRestore(0, 42, 1001, 1, ProvisionalStage.Drawing), Is.False);
        }

        [TestCase(ProvisionalStage.Drawing)]
        [TestCase(ProvisionalStage.Planning)]
        public void OpeningAllowsOnlyTheExactStageIdentity(ProvisionalStage stage)
        {
            var window = new StageSnapshotWindow();
            window.Open(42, 1001, 3, stage);
            Assert.That(window.CanRestore(window.Revision, 42, 1001, 3, stage), Is.True);
            Assert.That(window.CanRestore(window.Revision, 43, 1001, 3, stage), Is.False);
            Assert.That(window.CanRestore(window.Revision, 42, 1002, 3, stage), Is.False);
            Assert.That(window.CanRestore(window.Revision, 42, 1001, 4, stage), Is.False);
            Assert.That(
                window.CanRestore(
                    window.Revision,
                    42,
                    1001,
                    3,
                    stage == ProvisionalStage.Drawing
                        ? ProvisionalStage.Planning
                        : ProvisionalStage.Drawing
                ),
                Is.False
            );
        }

        [Test]
        public void CommitInvalidatesTheBaseline()
        {
            var window = new StageSnapshotWindow();
            window.Open(42, 1001, 1, ProvisionalStage.Drawing);
            ulong revision = window.Revision;
            window.Commit();
            Assert.That(
                window.CanRestore(revision, 42, 1001, 1, ProvisionalStage.Drawing),
                Is.False
            );
            window.Commit();
            Assert.That(window.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void ReopeningFencesStaleInputsEvenWithinTheSameStage()
        {
            var window = new StageSnapshotWindow();
            window.Open(42, 1001, 1, ProvisionalStage.Drawing);
            ulong abandoned = window.Revision;
            window.Open(42, 1001, 1, ProvisionalStage.Drawing);
            Assert.That(window.Revision, Is.GreaterThan(abandoned));
            Assert.That(
                window.CanRestore(abandoned, 42, 1001, 1, ProvisionalStage.Drawing),
                Is.False
            );
        }

        [Test]
        public void PlanningRequiresANewBaselineAndKeepsDrawingRevisionInvalid()
        {
            var window = new StageSnapshotWindow();
            window.Open(42, 1001, 1, ProvisionalStage.Drawing);
            ulong drawing = window.Revision;
            window.Commit();
            window.Open(42, 1001, 1, ProvisionalStage.Planning);
            Assert.That(
                window.CanRestore(drawing, 42, 1001, 1, ProvisionalStage.Drawing),
                Is.False
            );
            Assert.That(
                window.CanRestore(window.Revision, 42, 1001, 1, ProvisionalStage.Planning),
                Is.True
            );
        }

        [Test]
        public void RepeatedWindowsNeverReuseARevision()
        {
            var window = new StageSnapshotWindow();
            for (int i = 1; i <= 1000; i++)
            {
                window.Open(42, 1001, i, ProvisionalStage.Drawing);
                Assert.That(window.Revision, Is.EqualTo((ulong)i));
                window.Commit();
            }
        }
    }
}
