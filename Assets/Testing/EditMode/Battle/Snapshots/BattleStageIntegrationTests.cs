using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.Input;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Snapshots;
using Archeus.Battle.Systems.Actions;
using Archeus.Battle.Systems.Input;
using Archeus.Battle.Systems.Setup;
using Archeus.Battle.Systems.Turnflow;
using NUnit.Framework;
using Unity.Entities;

namespace Archeus.Testing.Battle.Snapshots
{
    public sealed class BattleStageIntegrationTests
    {
        public struct FutureRequest : IComponentData
        {
            public int Value;
        }

        private SnapshotTestFixture fixture;
        private EntityManager Entities => fixture.Entities;
        private SystemHandle drawing;
        private SystemHandle planning;
        private SystemHandle transition;

        [SetUp]
        public void SetUp()
        {
            fixture = new SnapshotTestFixture();
            drawing = fixture.World.CreateSystem<DrawingStageSystem>();
            planning = fixture.World.CreateSystem<PlanningStageSystem>();
            transition = fixture.World.CreateSystem<BattlePhaseTransitionSystem>();
        }

        [TearDown]
        public void TearDown() => fixture.Dispose();

        private void Request<T>(T value)
            where T : unmanaged, IComponentData
        {
            Entity request = Entities.CreateEntity();
            Entities.AddComponentData(request, value);
        }

        private void Draw()
        {
            Request(new DrawCardsRequest { Player = fixture.Player });
            drawing.Update(fixture.World.Unmanaged);
        }

        [Test]
        public void EntryWaitsForEffectsAndDoesNotCaptureOrDrawTwice()
        {
            Entities
                .GetBuffer<BattleEvent>(fixture.Battle)
                .Add(new BattleEvent { Source = fixture.Card });
            fixture.Open();
            Assert.That(
                Entities.GetComponentData<StageSnapshotStatus>(fixture.Battle).IsValid,
                Is.False
            );
            Draw();
            Assert.That(Entities.GetBuffer<HandCard>(fixture.Player).Length, Is.Zero);
            Entities.GetBuffer<BattleEvent>(fixture.Battle).Clear();
            ulong revision = fixture.Open();
            var rng = Entities.GetComponentData<BattleRNG>(fixture.Battle);
            fixture.Snapshots.Update();
            Assert.That(
                Entities.GetComponentData<StageSnapshotStatus>(fixture.Battle).Revision,
                Is.EqualTo(revision)
            );
            Assert.That(Entities.GetBuffer<HandCard>(fixture.Player).Length, Is.Zero);
            Assert.That(
                Entities.GetComponentData<BattleRNG>(fixture.Battle).StateA,
                Is.EqualTo(rng.StateA)
            );
        }

        [Test]
        public void MultipleDrawingActionsRevertAndReplenishIdentically()
        {
            Entity second = fixture.CreateCard(101);
            Entities.GetBuffer<DeckCard>(fixture.Player).Add(new DeckCard { Card = second });
            ulong revision = fixture.Open();
            var baseline = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            Draw();
            Entity firstDrawn = Entities.GetBuffer<HandCard>(fixture.Player)[0].Card;
            Request(
                new PlaceCardRequest
                {
                    Player = fixture.Player,
                    CardToPlace = firstDrawn,
                    Position = FieldPosition.AttackingForceSlot1,
                }
            );
            drawing.Update(fixture.World.Unmanaged);
            Request(new RetreatCardRequest { Player = fixture.Player, CardToRetreat = firstDrawn });
            drawing.Update(fixture.World.Unmanaged);
            Assert.That(
                Entities.GetComponentData<RemainingActionPoints>(fixture.Player).Value,
                Is.EqualTo(2)
            );
            Assert.That(Entities.HasComponent<RetreatedThisTurn>(firstDrawn), Is.True);
            Assert.That(fixture.Snapshots.TryRevert(fixture.Player, revision), Is.True);
            Assert.That(baseline.ComputeCurrentHash(Entities), Is.EqualTo(baseline.BaselineHash));
            Assert.That(Entities.HasComponent<RetreatedThisTurn>(firstDrawn), Is.False);
            Draw();
            Assert.That(
                Entities.GetBuffer<HandCard>(fixture.Player)[0].Card,
                Is.EqualTo(firstDrawn)
            );
        }

        [Test]
        public void PlanningRevertPreservesCommittedDrawingAndDoesNotRefillAP()
        {
            ulong drawingRevision = fixture.Open();
            Draw();
            Request(
                new PlaceCardRequest
                {
                    Player = fixture.Player,
                    CardToPlace = fixture.Card,
                    Position = FieldPosition.AttackingForceSlot1,
                }
            );
            drawing.Update(fixture.World.Unmanaged);
            Request(new EndDrawingRequest { Player = fixture.Player });
            drawing.Update(fixture.World.Unmanaged);
            Assert.That(fixture.Snapshots.TryRevert(fixture.Player, drawingRevision), Is.False);
            transition.Update(fixture.World.Unmanaged);
            ulong planningRevision = fixture.Open();
            var baseline = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            Entities.SetComponentData(fixture.Player, new RemainingActionPoints { Value = 0 });
            Entities.SetComponentData(fixture.Card, new CurrentHealth { Value = 12 });
            Entities
                .GetBuffer<ActionExecutionRequest>(fixture.Battle)
                .Add(
                    new ActionExecutionRequest
                    {
                        Source = fixture.Card,
                        PrimaryTarget = fixture.Card,
                        CharacterAction = CharacterActionType.NormalAttack,
                    }
                );
            Assert.That(fixture.Snapshots.TryRevert(fixture.Player, planningRevision), Is.True);
            Assert.That(
                Entities.GetBuffer<FieldCard>(fixture.Player)[0].Card,
                Is.EqualTo(fixture.Card)
            );
            Assert.That(
                Entities.GetComponentData<RemainingActionPoints>(fixture.Player).Value,
                Is.EqualTo(3)
            );
            Assert.That(Entities.GetBuffer<ActionExecutionRequest>(fixture.Battle).Length, Is.Zero);
            Assert.That(baseline.ComputeCurrentHash(Entities), Is.EqualTo(baseline.BaselineHash));
            Assert.That(fixture.Snapshots.TryRevert(fixture.Player, drawingRevision), Is.False);
        }

        [Test]
        public void PlanningCommitConsumesTheCorrectRequestAndInvalidatesSnapshot()
        {
            Entities.SetComponentData(
                fixture.Battle,
                new BattleState { Phase = BattlePhase.Planning }
            );
            ulong revision = fixture.Open();
            Request(new EndPlanningRequest { Player = fixture.Player });
            planning.Update(fixture.World.Unmanaged);
            Assert.That(Entities.HasComponent<BattlePlanningCompleteTag>(fixture.Battle), Is.True);
            Assert.That(fixture.Snapshots.TryRevert(fixture.Player, revision), Is.False);
            transition.Update(fixture.World.Unmanaged);
            fixture.Snapshots.Update();
            Assert.That(
                Entities.GetComponentData<BattleState>(fixture.Battle).Phase,
                Is.EqualTo(BattlePhase.Attacking)
            );
            Assert.That(
                Entities.GetComponentData<StageSnapshotStatus>(fixture.Battle).IsValid,
                Is.False
            );
        }

        [Test]
        public void PendingAttacksWaitForCommitAndAbandonedAttacksNeverExecute()
        {
            Entities.SetComponentData(
                fixture.Battle,
                new BattleState { Phase = BattlePhase.Planning }
            );
            ulong revision = fixture.Open();
            Entities
                .GetBuffer<ActionExecutionRequest>(fixture.Battle)
                .Add(
                    new ActionExecutionRequest
                    {
                        Source = fixture.Card,
                        PrimaryTarget = fixture.Card,
                        CharacterAction = CharacterActionType.NormalAttack,
                    }
                );
            var actions = fixture.World.CreateSystem<ActionExecutionSystem>();
            actions.Update(fixture.World.Unmanaged);
            Assert.That(
                Entities.GetBuffer<ActionExecutionRequest>(fixture.Battle).Length,
                Is.EqualTo(1)
            );
            Assert.That(fixture.Snapshots.TryRevert(fixture.Player, revision), Is.True);
            Entities.SetComponentData(
                fixture.Battle,
                new BattleState { Phase = BattlePhase.Attacking }
            );
            actions.Update(fixture.World.Unmanaged);
            Assert.That(Entities.GetBuffer<BattleEvent>(fixture.Battle).Length, Is.Zero);
            Assert.That(Entities.GetBuffer<ActionExecutionState>(fixture.Battle).Length, Is.Zero);
        }

        [Test]
        public void RevertInputWinsOverQueuedPlacementAndFencesDelayedInput()
        {
            ulong revision = fixture.Open();
            Entity inbox = Entities.CreateEntity();
            Entities.AddComponent<BattleInputInboxTag>(inbox);
            Entities.AddBuffer<BattleInputCommand>(inbox);
            var route = fixture.World.CreateSystem<BattleInputRoutingSystem>();
            var reverts = fixture.World.GetOrCreateSystemManaged<BattleStageRevertSystem>();
            var commands = Entities.GetBuffer<BattleInputCommand>(inbox);
            commands.Add(
                new BattleInputCommand
                {
                    BattleID = 42,
                    Side = BattleSide.SideA,
                    Type = BattleInputCommandType.DrawCards,
                    StageRevision = revision,
                }
            );
            commands.Add(
                new BattleInputCommand
                {
                    BattleID = 42,
                    Side = BattleSide.SideA,
                    Type = BattleInputCommandType.RevertStage,
                    StageRevision = revision,
                }
            );
            Request(new DrawCardsRequest { Player = fixture.Player });
            route.Update(fixture.World.Unmanaged);
            reverts.Update();
            drawing.Update(fixture.World.Unmanaged);
            Assert.That(Entities.GetBuffer<HandCard>(fixture.Player).Length, Is.Zero);
            Assert.That(
                Entities.GetComponentData<StageSnapshotStatus>(fixture.Battle).Revision,
                Is.GreaterThan(revision)
            );
            Entities
                .GetBuffer<BattleInputCommand>(inbox)
                .Add(
                    new BattleInputCommand
                    {
                        BattleID = 42,
                        Side = BattleSide.SideA,
                        Type = BattleInputCommandType.DrawCards,
                        StageRevision = revision,
                    }
                );
            route.Update(fixture.World.Unmanaged);
            drawing.Update(fixture.World.Unmanaged);
            Assert.That(Entities.GetBuffer<HandCard>(fixture.Player).Length, Is.Zero);
        }

        [Test]
        public void OpponentCannotRevertTheActivePlayersStage()
        {
            ulong revision = fixture.Open();
            Assert.That(fixture.Snapshots.TryRevert(fixture.Opponent, revision), Is.False);
            Assert.That(fixture.Snapshots.TryRevert(fixture.Player, revision + 1), Is.False);
        }

        [Test]
        public void NewRequestTypesInheritCancellationAndStaleGenerationFencing()
        {
            ulong revision = fixture.Open();
            Entity future = Entities.CreateEntity();
            Entities.AddComponentData(future, new FutureRequest { Value = 9 });
            Entities.AddComponentData(
                future,
                new StageCommandOwner { Battle = fixture.Battle, Revision = revision }
            );
            Request(
                new RevertStageRequest { Player = fixture.Player, ExpectedRevision = revision }
            );
            var reverts = fixture.World.GetOrCreateSystemManaged<BattleStageRevertSystem>();
            reverts.Update();
            Assert.That(Entities.Exists(future), Is.False);
            Entity delayed = Entities.CreateEntity();
            Entities.AddComponentData(delayed, new FutureRequest());
            Entities.AddComponentData(
                delayed,
                new StageCommandOwner { Battle = fixture.Battle, Revision = revision }
            );
            reverts.Update();
            Assert.That(Entities.Exists(delayed), Is.False);
        }

        [Test]
        public void InvalidOpponentRevertCannotSuppressAValidDrawingCommand()
        {
            ulong revision = fixture.Open();
            Entity inbox = Entities.CreateEntity();
            Entities.AddComponent<BattleInputInboxTag>(inbox);
            Entities.AddBuffer<BattleInputCommand>(inbox);
            var route = fixture.World.CreateSystem<BattleInputRoutingSystem>();
            var commands = Entities.GetBuffer<BattleInputCommand>(inbox);
            commands.Add(
                new BattleInputCommand
                {
                    BattleID = 42,
                    Side = BattleSide.SideA,
                    Type = BattleInputCommandType.DrawCards,
                    StageRevision = revision,
                }
            );
            commands.Add(
                new BattleInputCommand
                {
                    BattleID = 42,
                    Side = BattleSide.SideB,
                    Type = BattleInputCommandType.RevertStage,
                    StageRevision = revision,
                }
            );
            route.Update(fixture.World.Unmanaged);
            fixture.World.GetOrCreateSystemManaged<BattleStageRevertSystem>().Update();
            drawing.Update(fixture.World.Unmanaged);
            Assert.That(Entities.GetBuffer<HandCard>(fixture.Player).Length, Is.EqualTo(1));
        }

        [Test]
        public void AttackingWaitsForCommittedQueueAndCausalEffectsToDrain()
        {
            Entities.SetComponentData(
                fixture.Battle,
                new BattleState { Phase = BattlePhase.Attacking }
            );
            var attacking = fixture.World.CreateSystem<AttackingStageSystem>();
            var actions = fixture.World.CreateSystem<ActionExecutionSystem>();
            var request = new ActionExecutionRequest
            {
                Source = fixture.Card,
                PrimaryTarget = fixture.Card,
                CharacterAction = CharacterActionType.NormalAttack,
            };
            Entities.GetBuffer<ActionExecutionRequest>(fixture.Battle).Add(request);
            Entities.GetBuffer<ActionExecutionRequest>(fixture.Battle).Add(request);
            attacking.Update(fixture.World.Unmanaged);
            actions.Update(fixture.World.Unmanaged);
            transition.Update(fixture.World.Unmanaged);
            Assert.That(
                Entities.GetBuffer<ActionExecutionRequest>(fixture.Battle).Length,
                Is.EqualTo(1)
            );
            Assert.That(
                Entities.GetComponentData<BattleState>(fixture.Battle).Phase,
                Is.EqualTo(BattlePhase.Attacking)
            );
            // Represent completion by the event engine; the real VM/event engine has separate
            // integration coverage. The stage must still wait for the next queued attack.
            Entities.GetBuffer<BattleEvent>(fixture.Battle).Clear();
            Entities.GetBuffer<ActionExecutionState>(fixture.Battle).Clear();
            attacking.Update(fixture.World.Unmanaged);
            actions.Update(fixture.World.Unmanaged);
            Assert.That(
                Entities.HasComponent<BattleAttackingCompleteTag>(fixture.Battle),
                Is.False
            );
            Entities.GetBuffer<BattleEvent>(fixture.Battle).Clear();
            Entities.GetBuffer<ActionExecutionState>(fixture.Battle).Clear();
            Entities.GetBuffer<BehaviourExecutionRequest>(fixture.Battle).Add(default);
            attacking.Update(fixture.World.Unmanaged);
            Assert.That(
                Entities.HasComponent<BattleAttackingCompleteTag>(fixture.Battle),
                Is.False
            );
            Entities.GetBuffer<BehaviourExecutionRequest>(fixture.Battle).Clear();
            attacking.Update(fixture.World.Unmanaged);
            transition.Update(fixture.World.Unmanaged);
            Assert.That(
                Entities.GetComponentData<BattleState>(fixture.Battle).Phase,
                Is.EqualTo(BattlePhase.TurnEnd)
            );
        }
    }
}
