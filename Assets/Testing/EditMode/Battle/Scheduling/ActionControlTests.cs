using System.Linq;
using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Scheduling;
using NUnit.Framework;
using Unity.Entities;

namespace Archeus.Testing.Battle.Scheduling
{
    public sealed class ActionControlTests
    {
        [Test]
        public void TypedPendingOperationsUseStableIDsAndPreserveDeterministicOrder()
        {
            using var f = new SchedulerTestFixture(false);
            uint first = f.Plan(f.Mei, f.Enemy),
                second = f.Plan(f.Toma, f.Enemy);
            var control = f.Control(ActionControlKind.InsertNext, f.Harun);
            f.Apply(control);
            uint front = f.EM.GetBuffer<PlannedAction>(f.Context)[0].QueueActionID;
            control = f.Control(ActionControlKind.Append, f.Mei);
            f.Apply(control);
            uint tail = f.EM.GetBuffer<PlannedAction>(f.Context)[3].QueueActionID;
            control = f.Control(ActionControlKind.InsertAt, f.Mei);
            control.Position = 2;
            f.Apply(control);
            uint middle = f.EM.GetBuffer<PlannedAction>(f.Context)[2].QueueActionID;
            control = f.Control(ActionControlKind.InsertAfter, f.Mei);
            control.RelativeQueueActionID = second;
            f.Apply(control);
            uint after = f.EM.GetBuffer<PlannedAction>(f.Context)[4].QueueActionID;
            CollectionAssert.AreEqual(
                new[] { front, first, middle, second, after, tail },
                f.Projection.Contexts.Single().Pending.Select(x => x.QueueActionID)
            );
            control = f.Control(ActionControlKind.MovePending);
            control.TargetQueueActionID = tail;
            control.Position = 0;
            f.Apply(control);
            control = f.Control(ActionControlKind.RemovePending);
            control.TargetQueueActionID = middle;
            f.Apply(control);
            control = f.Control(ActionControlKind.ReplacePending, f.Harun);
            control.TargetQueueActionID = second;
            f.Apply(control);
            var pending = f.Projection.Contexts.Single().Pending;
            CollectionAssert.AreEqual(
                new[] { tail, front, first, after },
                pending
                    .Where(x => x.QueueActionID != pending[3].QueueActionID)
                    .Select(x => x.QueueActionID)
            );
            Assert.That(pending[3].SourceRuntimeID, Is.EqualTo(102));
            Assert.That(pending[3].QueueActionID, Is.Not.EqualTo(second));
            Assert.That(
                pending.All(x => x.QueueActionID != middle && x.QueueActionID != second),
                Is.True
            );
        }

        [Test]
        public void DuplicateAndInvalidRequestsDoNotSpendOrMutate()
        {
            using var f = new SchedulerTestFixture(false);
            var request = f.Control(ActionControlKind.Append);
            Assert.That(
                ActionControlProcessor.Enqueue(f.EM, f.Battle, request, out ulong id),
                Is.True
            );
            request.RequestID = id;
            Assert.That(ActionControlProcessor.Enqueue(f.EM, f.Battle, request, out _), Is.True);
            ActionControlProcessor.ApplyFrame(f.EM, f.Battle, 0);
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.EqualTo(1));
            Assert.That(f.EM.GetBuffer<ActionControlResult>(f.Battle).Length, Is.EqualTo(1));
            var before = ActionOrderPublication.Fingerprint(f.Projection);
            request = f.Control(ActionControlKind.ReplacePending);
            request.TargetQueueActionID = 999;
            f.Apply(request);
            request = f.Control(ActionControlKind.MovePending);
            request.TargetQueueActionID = 1;
            request.Position = -1;
            f.Apply(request);
            request = f.Control(ActionControlKind.InsertAfter);
            request.RelativeQueueActionID = 999;
            f.Apply(request);
            request = f.Control(ActionControlKind.InsertAt);
            request.Position = 100;
            f.Apply(request);
            Assert.That(ActionOrderPublication.Fingerprint(f.Projection), Is.EqualTo(before));
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(4)
            );
        }

        [Test]
        public void SameBoundaryFrontInsertsFollowPriorityThenRuntimeIdentity()
        {
            using var f = new SchedulerTestFixture(false);
            f.Plan(f.Toma, f.Enemy);
            var low = f.Control(ActionControlKind.InsertNext, f.Mei);
            low.Priority = 1;
            low.OwnerRuntimeID = 100;
            var high = f.Control(ActionControlKind.InsertNext, f.Harun);
            high.Priority = 2;
            high.OwnerRuntimeID = 102;
            ActionControlProcessor.Enqueue(f.EM, f.Battle, low, out _);
            ActionControlProcessor.Enqueue(f.EM, f.Battle, high, out _);
            ActionControlProcessor.ApplyFrame(f.EM, f.Battle, 0);
            CollectionAssert.AreEqual(
                new uint[] { 102, 100, 101 },
                f.Projection.Contexts.Single().Pending.Select(x => x.SourceRuntimeID)
            );
        }

        [Test]
        public void ManualEligibilityResourcesRefundsAndAutomaticEligibilityAreSeparate()
        {
            using var f = new SchedulerTestFixture(false);
            var action = new PlannedAction
            {
                ActionType = PlannedActionType.CharacterAction,
                Source = f.Karathos,
                PrimaryTarget = f.Enemy,
                CharacterAction = CharacterActionType.SuperchargedAttack,
            };
            Assert.That(
                ActionQueueService.TryPlan(f.EM, f.Battle, f.Player, action, out _),
                Is.False
            );
            var automatic = f.Control(ActionControlKind.Append, f.Karathos);
            automatic.Action.CharacterAction = CharacterActionType.SuperchargedAttack;
            f.Apply(automatic);
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.EqualTo(1));
            f.EM.SetComponentData(f.Toma, new HeldSuperchargedEnergy { Value = 0 });
            action.Source = f.Toma;
            Assert.That(
                ActionQueueService.TryPlan(f.EM, f.Battle, f.Player, action, out _),
                Is.False
            );
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(4)
            );
            uint manual = f.Plan(f.Mei, f.Enemy);
            Assert.That(
                ActionQueueService.TryRemove(f.EM, f.Battle, f.Context, manual, true),
                Is.True
            );
            Assert.That(
                ActionQueueService.TryRemove(f.EM, f.Battle, f.Context, manual, true),
                Is.False
            );
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(4)
            );
        }

        [Test]
        public void PendingEnemySelectionRequiresOfferPlayerContextEpochAndPendingID()
        {
            using var f = new SchedulerTestFixture(false);
            uint removed = f.Plan(f.Mei, f.Enemy),
                remains = f.Plan(f.Harun, f.Enemy);
            Assert.That(
                ActionControlProcessor.SelectInterrupt(f.EM, f.Battle, f.Opponent, 1, removed),
                Is.False
            );
            var offer = f.Control(ActionControlKind.OfferInterruptSelection);
            offer.TargetPlayer = f.Opponent;
            f.Apply(offer);
            uint selection = f.EM.GetBuffer<PendingInterruptSelection>(f.Battle)[0].SelectionID;
            Assert.That(
                ActionControlProcessor.SelectInterrupt(
                    f.EM,
                    f.Battle,
                    f.Player,
                    selection,
                    removed
                ),
                Is.False
            );
            Assert.That(
                ActionControlProcessor.SelectInterrupt(f.EM, f.Battle, f.Opponent, selection, 999),
                Is.False
            );
            Assert.That(
                ActionControlProcessor.SelectInterrupt(
                    f.EM,
                    f.Battle,
                    f.Opponent,
                    selection,
                    removed
                ),
                Is.True
            );
            Assert.That(
                ActionControlProcessor.SelectInterrupt(
                    f.EM,
                    f.Battle,
                    f.Opponent,
                    selection,
                    remains
                ),
                Is.False
            );
            f.Commit();
            f.Dispatch.Update(f.World.Unmanaged);
            Assert.That(
                ActionControlProcessor.SelectInterrupt(
                    f.EM,
                    f.Battle,
                    f.Opponent,
                    selection,
                    remains
                ),
                Is.False
            );
            f.Events.Update(f.World.Unmanaged);
            Assert.That(
                ActionControlProcessor.SelectInterrupt(
                    f.EM,
                    f.Battle,
                    f.Opponent,
                    selection,
                    remains
                ),
                Is.False
            );
            Assert.That(f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle).Length, Is.EqualTo(1));
            Assert.That(
                f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle)[0].QueueActionID,
                Is.EqualTo(remains)
            );
        }

        [Test]
        public void SelectionBlocksCommitUntilResolvedAndStaleEpochCannotSelect()
        {
            using var f = new SchedulerTestFixture(false);
            uint id = f.Plan(f.Mei, f.Enemy);
            var offer = f.Control(ActionControlKind.OfferInterruptSelection);
            offer.TargetPlayer = f.Opponent;
            f.Apply(offer);
            var saved = f.EM.GetBuffer<PendingInterruptSelection>(f.Battle)[0];
            f.EM.AddComponent<Archeus.Battle.Components.Tags.BattlePlanningCompleteTag>(f.Battle);
            f.Phases.Update(f.World.Unmanaged);
            Assert.That(
                f.EM.GetComponentData<BattleState>(f.Battle).Phase,
                Is.EqualTo(BattlePhase.Planning)
            );
            f.Phase(BattlePhase.Planning);
            Assert.That(
                ActionControlProcessor.SelectInterrupt(
                    f.EM,
                    f.Battle,
                    f.Opponent,
                    saved.SelectionID,
                    id
                ),
                Is.False
            );
        }

        [TestCase(ActionTargetMode.Self)]
        [TestCase(ActionTargetMode.None)]
        [TestCase(ActionTargetMode.AllEnemies)]
        public void BoundActionsRepresentNonSingleTargetsAndExecuteWithoutNullDereferences(
            ActionTargetMode mode
        )
        {
            using var f = new SchedulerTestFixture(false);
            f.EM.AddBuffer<ActionProgramBinding>(f.Mei)
                .Add(
                    new ActionProgramBinding
                    {
                        Type = CharacterActionType.Transformation,
                        DefinitionID = 77,
                        TargetMode = mode,
                        ProgramIndex = 3,
                    }
                );
            f.Plan(f.Mei, Entity.Null, CharacterActionType.Transformation, 77);
            f.Commit();
            f.RunAction();
            Assert.That(
                f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle)[0].Outcome,
                Is.EqualTo(ActionOutcome.Completed)
            );
            Assert.That(
                f.HP(f.Enemy),
                Is.EqualTo(mode == ActionTargetMode.AllEnemies ? 980 : 1000)
            );
            Assert.That(f.HP(f.Mei), Is.EqualTo(mode == ActionTargetMode.Self ? 980 : 1000));
        }

        [Test]
        public void ExplicitDispatchAndContextBoundsHaltWithoutConsumingQueue()
        {
            using var f = new SchedulerTestFixture(false);
            f.Plan(f.Mei, f.Enemy);
            f.Commit();
            var scheduler = f.EM.GetComponentData<ActionSchedulerState>(f.Battle);
            scheduler.DispatchesThisTurn = ActionQueueAlgorithms.MaximumDispatchesPerTurn;
            f.EM.SetComponentData(f.Battle, scheduler);
            f.Dispatch.Update(f.World.Unmanaged);
            Assert.That(
                f.EM.GetComponentData<ActionSchedulerState>(f.Battle).Failure,
                Is.EqualTo(SchedulerFailure.DispatchLimit)
            );
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.EqualTo(1));
            Assert.That(f.EM.GetBuffer<ActionExecutionState>(f.Battle).Length, Is.Zero);
        }
    }
}
