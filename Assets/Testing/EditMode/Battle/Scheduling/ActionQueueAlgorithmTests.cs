using System;
using System.Collections.Generic;
using System.Linq;
using Archeus.Battle.Scheduling;
using NUnit.Framework;

namespace Archeus.Testing.Battle.Scheduling
{
    public sealed class ActionQueueAlgorithmTests
    {
        private struct Action : IStableQueuedAction
        {
            public uint StableID { get; set; }
        }

        private struct Queue : IActionQueueStorage<Action>
        {
            public List<Action> Items;
            public int Count => Items.Count;
            public Action this[int i]
            {
                get => Items[i];
                set => Items[i] = value;
            }

            public void Add(Action a) => Items.Add(a);

            public void RemoveAt(int i) => Items.RemoveAt(i);
        }

        private Queue queue;

        [SetUp]
        public void Setup() => queue = new Queue { Items = new List<Action>() };

        private bool Insert(uint id, int index) =>
            ActionQueueAlgorithms.Insert<Action, Queue>(
                ref queue,
                new Action { StableID = id },
                index
            );

        private uint[] IDs => queue.Items.Select(x => x.StableID).ToArray();

        [Test]
        public void FrontAppendMiddleAndAfterStableIDPreserveExpectedOrder()
        {
            Assert.That(Insert(10, 0), Is.True);
            Assert.That(Insert(20, 1), Is.True);
            Assert.That(Insert(30, 0), Is.True);
            Assert.That(
                Insert(40, ActionQueueAlgorithms.Find<Action, Queue>(ref queue, 10) + 1),
                Is.True
            );
            CollectionAssert.AreEqual(new uint[] { 30, 10, 40, 20 }, IDs);
        }

        [Test]
        public void RemoveAndMoveKeepIdentityAndRejectInvalidOperationsAtomically()
        {
            for (uint id = 1; id <= 3; id++)
                Insert(id, queue.Count);
            Assert.That(ActionQueueAlgorithms.Move<Action, Queue>(ref queue, 1, 2), Is.True);
            CollectionAssert.AreEqual(new uint[] { 2, 3, 1 }, IDs);
            Assert.That(
                ActionQueueAlgorithms.Remove<Action, Queue>(ref queue, 3, out var removed),
                Is.True
            );
            Assert.That(removed.StableID, Is.EqualTo(3));
            Assert.That(Insert(2, 0), Is.False);
            Assert.That(Insert(0, 0), Is.False);
            Assert.That(Insert(4, -1), Is.False);
            Assert.That(Insert(4, 3), Is.False);
            Assert.That(ActionQueueAlgorithms.Move<Action, Queue>(ref queue, 99, 0), Is.False);
            Assert.That(ActionQueueAlgorithms.Move<Action, Queue>(ref queue, 1, 2), Is.False);
            Assert.That(ActionQueueAlgorithms.Remove<Action, Queue>(ref queue, 3, out _), Is.False);
            CollectionAssert.AreEqual(new uint[] { 2, 1 }, IDs);
        }

        [Test]
        public void CapacityIsBoundedAndDequeuesShrinkUntilEmpty()
        {
            for (uint id = 1; id <= ActionQueueAlgorithms.MaximumPendingActions; id++)
                Assert.That(Insert(id, queue.Count), Is.True);
            Assert.That(Insert(999, 0), Is.False);
            for (uint id = 1; id <= ActionQueueAlgorithms.MaximumPendingActions; id++)
            {
                Assert.That(queue[0].StableID, Is.EqualTo(id));
                Assert.That(
                    ActionQueueAlgorithms.Remove<Action, Queue>(ref queue, id, out _),
                    Is.True
                );
            }
            Assert.That(queue.Count, Is.Zero);
        }

        [TestCase(123)]
        [TestCase(456)]
        [TestCase(789)]
        public void MixedMutationsMatchIndependentListModel(int seed)
        {
            var random = new Random(seed);
            var expected = new List<uint>();
            uint next = 1;
            for (int step = 0; step < 2000; step++)
            {
                int op = expected.Count == 0 ? 0 : random.Next(3);
                int position = random.Next(expected.Count + 1);
                if (op == 0 && expected.Count < 200)
                {
                    Assert.That(Insert(next, position), Is.True);
                    expected.Insert(position, next++);
                }
                else if (op == 1)
                {
                    int old = random.Next(expected.Count);
                    uint id = expected[old];
                    Assert.That(
                        ActionQueueAlgorithms.Remove<Action, Queue>(ref queue, id, out _),
                        Is.True
                    );
                    expected.RemoveAt(old);
                }
                else if (expected.Count > 0)
                {
                    int old = random.Next(expected.Count);
                    int target = random.Next(expected.Count);
                    uint id = expected[old];
                    Assert.That(
                        ActionQueueAlgorithms.Move<Action, Queue>(ref queue, id, target),
                        Is.True
                    );
                    expected.RemoveAt(old);
                    expected.Insert(target, id);
                }
                CollectionAssert.AreEqual(expected, IDs, $"seed={seed}, step={step}");
            }
        }

        [Test]
        public void ControlOrderUsesEveryDocumentedTieBreak()
        {
            Assert.That(
                ActionQueueAlgorithms.CompareControl(20, 2, 3, 4, 5, 10, 1, 1, 1, 1),
                Is.LessThan(0)
            );
            Assert.That(
                ActionQueueAlgorithms.CompareControl(20, 1, 9, 9, 9, 20, 2, 1, 1, 1),
                Is.LessThan(0)
            );
            Assert.That(
                ActionQueueAlgorithms.CompareControl(20, 1, 1, 9, 9, 20, 1, 2, 1, 1),
                Is.LessThan(0)
            );
            Assert.That(
                ActionQueueAlgorithms.CompareControl(20, 1, 1, 1, 9, 20, 1, 1, 2, 1),
                Is.LessThan(0)
            );
            Assert.That(
                ActionQueueAlgorithms.CompareControl(20, 1, 1, 1, 1, 20, 1, 1, 1, 2),
                Is.LessThan(0)
            );
            Assert.That(
                ActionQueueAlgorithms.CompareControl(20, 1, 1, 1, 1, 20, 1, 1, 1, 1),
                Is.Zero
            );
        }

        [TestCase(ActionOutcome.Queued, false)]
        [TestCase(ActionOutcome.AttemptStarted, true)]
        [TestCase(ActionOutcome.Authorized, false)]
        [TestCase(ActionOutcome.ConsequencesApplied, false)]
        [TestCase(ActionOutcome.Completed, false)]
        [TestCase(ActionOutcome.Interrupted, false)]
        [TestCase(ActionOutcome.Cancelled, false)]
        [TestCase(ActionOutcome.Failed, false)]
        public void OnlyPreauthorizationAttemptsCanBeInterrupted(
            ActionOutcome outcome,
            bool expected
        ) => Assert.That(ActionQueueAlgorithms.CanInterrupt(outcome), Is.EqualTo(expected));
    }
}
