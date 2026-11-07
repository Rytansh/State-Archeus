using System.Linq;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Runtime;
using Archeus.Battle.Scheduling;
using Archeus.Battle.Snapshots;
using Archeus.Battle.Systems.Effects;
using Archeus.Battle.Systems.Turnflow;
using NUnit.Framework;
using Unity.Entities;

namespace Archeus.Testing.Battle.Scheduling
{
    public sealed class ExecutionContextIntegrationTests
    {
        private static void Request<T>(SchedulerTestFixture f, T request)
            where T : unmanaged, IComponentData
        {
            Entity entity = f.EM.CreateEntity();
            f.EM.AddComponentData(entity, request);
        }

        private static Entity BeginChild(
            SchedulerTestFixture f,
            ActionControlKind kind,
            Entity actor
        )
        {
            f.Apply(f.Control(kind, actor));
            f.Phases.Update(f.World.Unmanaged);
            f.Snapshot.Snapshots.Update();
            return f.Context;
        }

        [TestCase(ActionControlKind.ExtraAction, BattlePhase.Planning, 1)]
        [TestCase(ActionControlKind.ExtraTurn, BattlePhase.Drawing, 4)]
        public void RealVMReactionRequestsChildOnlyAfterOriginalActionSettles(
            ActionControlKind kind,
            BattlePhase entry,
            int ap
        )
        {
            using var f = new SchedulerTestFixture(true, kind);
            var eligible = f.EM.GetComponentData<ActionEligibility>(f.Karathos);
            eligible.ManualMask = ActionEligibility.Mask(CharacterActionType.NormalAttack);
            f.EM.SetComponentData(f.Karathos, eligible);
            f.Plan(f.Toma, f.Enemy, CharacterActionType.SuperchargedAttack);
            f.Plan(f.Harun, f.Enemy);
            f.Commit();
            Entity parent = f.Context;
            f.Dispatch.Update(f.World.Unmanaged);
            f.Phases.Update(f.World.Unmanaged);
            Assert.That(f.Context, Is.EqualTo(parent), "No handoff while attempt events remain.");
            f.Events.Update(f.World.Unmanaged);
            f.Phases.Update(f.World.Unmanaged);
            f.Snapshot.Snapshots.Update();
            Assert.That(f.Context, Is.Not.EqualTo(parent));
            Assert.That(f.EM.GetComponentData<BattleState>(f.Battle).Phase, Is.EqualTo(entry));
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(ap)
            );
            Assert.That(
                f.EM.GetComponentData<AttackingStageExecutionContext>(f.Context).ActingCharacter,
                Is.EqualTo(f.Karathos)
            );
            Assert.That(f.HP(f.Enemy), Is.EqualTo(960));
            Assert.That(f.EM.GetBuffer<PlannedAction>(parent).Length, Is.EqualTo(1));
            Assert.That(f.EM.GetComponentData<TurnCounter>(f.Battle).CurrentTurn, Is.EqualTo(1));
        }

        [Test]
        public void ExtraActionIsOneAPRestrictedPlanningAndResumesParentQueueAndAP()
        {
            using var f = new SchedulerTestFixture(false);
            f.Plan(f.Mei, f.Enemy);
            uint harun = f.Plan(f.Harun, f.Enemy);
            f.Commit();
            f.RunAction();
            Entity parent = f.Context;
            int turn = f.EM.GetComponentData<TurnCounter>(f.Battle).CurrentTurn;
            Entity child = BeginChild(f, ActionControlKind.ExtraAction, f.Mei);
            Assert.That(child, Is.Not.EqualTo(parent));
            Assert.That(
                f.EM.GetComponentData<BattleState>(f.Battle).Phase,
                Is.EqualTo(BattlePhase.Planning)
            );
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(1)
            );
            Assert.That(
                f.EM.GetComponentData<AttackingStageExecutionContext>(parent).ExecutionStatus,
                Is.EqualTo(AttackingStageExecutionStatus.Suspended)
            );
            var forbidden = new PlannedAction
            {
                ActionType = PlannedActionType.CharacterAction,
                Source = f.Harun,
                PrimaryTarget = f.Enemy,
                CharacterAction = CharacterActionType.NormalAttack,
            };
            Assert.That(
                ActionQueueService.TryPlan(f.EM, f.Battle, f.Player, forbidden, out _),
                Is.False
            );
            f.Plan(f.Mei, f.Enemy); // The parent's target history does not exclude it in this child.
            forbidden.Source = f.Mei;
            Assert.That(
                ActionQueueService.TryPlan(f.EM, f.Battle, f.Player, forbidden, out _),
                Is.False
            );
            f.Commit();
            f.RunAction();
            f.FinishStage();
            Assert.That(f.Context, Is.EqualTo(parent));
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(2)
            );
            Assert.That(f.EM.GetBuffer<PlannedAction>(parent)[0].QueueActionID, Is.EqualTo(harun));
            Assert.That(f.EM.GetComponentData<TurnCounter>(f.Battle).CurrentTurn, Is.EqualTo(turn));
            f.RunAction();
            Assert.That(f.HP(f.Enemy), Is.EqualTo(950));
            Assert.That(f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle).Length, Is.EqualTo(3));
        }

        [Test]
        public void ExtraActionCannotCommitEmptyOrWithAnotherManualActor()
        {
            using var f = new SchedulerTestFixture(false);
            f.Commit();
            BeginChild(f, ActionControlKind.ExtraAction, f.Mei);
            Request(f, new EndPlanningRequest { Player = f.Player });
            f.Planning.Update(f.World.Unmanaged);
            f.Phases.Update(f.World.Unmanaged);
            Assert.That(
                f.EM.GetComponentData<BattleState>(f.Battle).Phase,
                Is.EqualTo(BattlePhase.Planning)
            );
            Assert.That(f.EM.HasComponent<BattlePlanningCompleteTag>(f.Battle), Is.False);
            f.Plan(f.Mei, f.Enemy);
            f.Commit();
            Assert.That(
                f.EM.GetComponentData<BattleState>(f.Battle).Phase,
                Is.EqualTo(BattlePhase.Attacking)
            );
        }

        [Test]
        public void ExtraTurnDrawsWithFourAPAndNeverRunsNormalTurnHooksOrDurations()
        {
            using var f = new SchedulerTestFixture(false);
            Entity deckCard = f.Snapshot.CreateCard(300);
            f.EM.GetBuffer<DeckCard>(f.Player).Add(new DeckCard { Card = deckCard });
            f.EM.GetBuffer<ActiveEffect>(f.Mei)
                .Add(new ActiveEffect { EffectIndex = 0, RemainingDuration = 3 });
            f.Plan(f.Mei, f.Enemy);
            f.Plan(f.Harun, f.Enemy);
            f.Commit();
            f.RunAction();
            Entity parent = f.Context;
            int turn = f.EM.GetComponentData<TurnCounter>(f.Battle).CurrentTurn;
            BeginChild(f, ActionControlKind.ExtraTurn, f.Toma);
            Assert.That(
                f.EM.GetComponentData<BattleState>(f.Battle).Phase,
                Is.EqualTo(BattlePhase.Drawing)
            );
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(4)
            );
            var start = f.World.CreateSystem<TurnStartSystem>();
            var end = f.World.CreateSystem<TurnEndSystem>();
            var duration = f.World.CreateSystem<EffectDurationProcessingSystem>();
            start.Update(f.World.Unmanaged);
            end.Update(f.World.Unmanaged);
            duration.Update(f.World.Unmanaged);
            Assert.That(f.EM.GetBuffer<BattleEvent>(f.Battle).Length, Is.Zero);
            Request(f, new DrawCardsRequest { Player = f.Player });
            f.Drawing.Update(f.World.Unmanaged);
            Assert.That(f.EM.GetBuffer<HandCard>(f.Player)[0].Card, Is.EqualTo(deckCard));
            Request(
                f,
                new PlaceCardRequest
                {
                    Player = f.Player,
                    CardToPlace = deckCard,
                    Position = FieldPosition.AttackingForceSlot1,
                }
            );
            f.Drawing.Update(f.World.Unmanaged);
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(3)
            );
            Request(f, new EndDrawingRequest { Player = f.Player });
            f.Drawing.Update(f.World.Unmanaged);
            f.Phases.Update(f.World.Unmanaged);
            f.Snapshot.Snapshots.Update();
            f.Plan(f.Toma, f.Enemy, CharacterActionType.SuperchargedAttack);
            f.Commit();
            f.RunAction();
            f.FinishStage();
            Assert.That(
                f.EM.GetBuffer<BehaviourRuntimeState>(f.Toma)[0].Memory[0],
                Is.EqualTo(1),
                "Action hooks still run in children."
            );
            Assert.That(f.Context, Is.EqualTo(parent));
            Assert.That(f.EM.GetComponentData<TurnCounter>(f.Battle).CurrentTurn, Is.EqualTo(turn));
            Assert.That(f.EM.GetBuffer<ActiveEffect>(f.Mei)[0].RemainingDuration, Is.EqualTo(3));
            Assert.That(f.EM.HasComponent<EffectDurationsProcessingTag>(f.Battle), Is.False);
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(2)
            );
        }

        [Test]
        public void NestedExtraTurnAndExtraActionResumeInLifoOrderAcrossPlayers()
        {
            using var f = new SchedulerTestFixture(false);
            f.Plan(f.Mei, f.Enemy);
            f.Commit();
            Entity root = f.Context;
            Entity extraTurn = BeginChild(f, ActionControlKind.ExtraTurn, f.Enemy);
            Assert.That(
                f.EM.GetComponentData<ActiveTurnPlayer>(f.Battle).Player,
                Is.EqualTo(f.Opponent)
            );
            Request(f, new EndDrawingRequest { Player = f.Opponent });
            f.Drawing.Update(f.World.Unmanaged);
            f.Phases.Update(f.World.Unmanaged);
            f.Snapshot.Snapshots.Update();
            f.Plan(f.Enemy, f.Mei);
            f.Commit();
            Entity extraAction = BeginChild(f, ActionControlKind.ExtraAction, f.Mei);
            f.Plan(f.Mei, f.Enemy);
            f.Commit();
            f.RunAction();
            f.FinishStage();
            Assert.That(f.Context, Is.EqualTo(extraTurn));
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Opponent).Value,
                Is.EqualTo(3)
            );
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.EqualTo(1));
            f.RunAction();
            f.FinishStage();
            Assert.That(f.Context, Is.EqualTo(root));
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(3)
            );
            Assert.That(
                f.EM.GetComponentData<AttackingStageExecutionContext>(extraAction).ExecutionStatus,
                Is.EqualTo(AttackingStageExecutionStatus.Completed)
            );
            Assert.That(f.EM.GetComponentData<TurnCounter>(f.Battle).CurrentTurn, Is.EqualTo(1));
            f.RunAction();
            Assert.That(f.HP(f.Enemy), Is.EqualTo(980));
        }

        [Test]
        public void ChildPlanningReversionRestoresOnlyChildBaselineAndRemapsDestroyedParent()
        {
            using var f = new SchedulerTestFixture(false);
            f.Plan(f.Mei, f.Enemy);
            f.Plan(f.Harun, f.Enemy);
            f.Commit();
            f.RunAction();
            Entity parent = f.Context;
            Entity child = BeginChild(f, ActionControlKind.ExtraAction, f.Mei);
            uint childID = f.ContextID;
            ulong revision = f.Snapshot.Open();
            f.Plan(f.Mei, f.Enemy);
            f.EM.DestroyEntity(parent);
            Assert.That(f.Snapshot.Snapshots.TryRevert(f.Player, revision), Is.True);
            Assert.That(f.ContextID, Is.EqualTo(childID));
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.Zero);
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(1)
            );
            Assert.That(f.HP(f.Enemy), Is.EqualTo(990));
            Entity remapped = f
                .EM.GetComponentData<AttackingStageExecutionContext>(f.Context)
                .ParentContext;
            Assert.That(f.EM.Exists(remapped), Is.True);
            Assert.That(f.EM.GetBuffer<PlannedAction>(remapped).Length, Is.EqualTo(1));
            Assert.That(f.EM.GetComponentData<OwnedBattle>(remapped).Battle, Is.EqualTo(f.Battle));
            f.Plan(f.Mei, f.Enemy);
            f.Commit();
            f.RunAction();
            f.FinishStage();
            Assert.That(f.Context, Is.EqualTo(remapped));
            f.RunAction();
            Assert.That(f.HP(f.Enemy), Is.EqualTo(950));
        }

        [Test]
        public void ExtraTurnDrawingReversionRestoresFourAPAndItsStageIdentity()
        {
            using var f = new SchedulerTestFixture(false);
            Entity card = f.Snapshot.CreateCard(300);
            f.EM.GetBuffer<DeckCard>(f.Player).Add(new DeckCard { Card = card });
            f.Commit();
            BeginChild(f, ActionControlKind.ExtraTurn, f.Mei);
            uint child = f.ContextID;
            ulong revision = f.Snapshot.Open();
            Request(f, new DrawCardsRequest { Player = f.Player });
            f.Drawing.Update(f.World.Unmanaged);
            Assert.That(f.EM.GetBuffer<HandCard>(f.Player).Length, Is.EqualTo(1));
            Assert.That(f.Snapshot.Snapshots.TryRevert(f.Player, revision), Is.True);
            Assert.That(f.ContextID, Is.EqualTo(child));
            Assert.That(f.EM.GetBuffer<HandCard>(f.Player).Length, Is.Zero);
            Assert.That(f.EM.GetBuffer<DeckCard>(f.Player).Length, Is.EqualTo(1));
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Player).Value,
                Is.EqualTo(4)
            );
        }

        [Test]
        public void SkipAttackDiscardsPendingWorkBeforeDispatchAndKeepsContextConsistent()
        {
            using var f = new SchedulerTestFixture(false);
            f.Plan(f.Mei, f.Enemy);
            var skip = f.Control(ActionControlKind.SkipStages);
            skip.TargetPlayer = f.Player;
            skip.Stages = BattleStageMask.Attacking;
            f.Apply(skip);
            Request(f, new EndPlanningRequest { Player = f.Player });
            f.Planning.Update(f.World.Unmanaged);
            f.Phases.Update(f.World.Unmanaged);
            Assert.That(
                f.EM.GetComponentData<BattleState>(f.Battle).Phase,
                Is.EqualTo(BattlePhase.TurnEnd)
            );
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.Zero);
            f.Dispatch.Update(f.World.Unmanaged);
            Assert.That(f.HP(f.Enemy), Is.EqualTo(1000));
            Assert.That(f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle).Length, Is.Zero);
        }

        [Test]
        public void SkipReceivedBeforeDrawingWorkRejectsQueuedDrawingOperation()
        {
            using var f = new SchedulerTestFixture(false);
            Entity card = f.Snapshot.CreateCard(300);
            f.EM.GetBuffer<DeckCard>(f.Player).Add(new DeckCard { Card = card });
            f.Phase(BattlePhase.Drawing);
            f.Snapshot.Open();
            var skip = f.Control(ActionControlKind.SkipStages);
            skip.TargetPlayer = f.Player;
            skip.Stages = BattleStageMask.Drawing;
            f.Apply(skip);
            Request(f, new DrawCardsRequest { Player = f.Player });
            f.Drawing.Update(f.World.Unmanaged);
            f.Phases.Update(f.World.Unmanaged);
            Assert.That(f.EM.GetBuffer<HandCard>(f.Player).Length, Is.Zero);
            Assert.That(f.EM.GetBuffer<DeckCard>(f.Player).Length, Is.EqualTo(1));
            Assert.That(
                f.EM.GetComponentData<BattleState>(f.Battle).Phase,
                Is.EqualTo(BattlePhase.Planning)
            );
        }

        [Test]
        public void SkippedOpponentTurnConsumesCountWithoutStartEndOperationsAndReturnsToCorrectPlayer()
        {
            using var f = new SchedulerTestFixture(false);
            f.Plan(f.Mei, f.Enemy);
            f.Commit();
            var skip = f.Control(ActionControlKind.SkipTurn);
            skip.TargetPlayer = f.Opponent;
            f.Apply(skip);
            f.EM.SetComponentData(f.Opponent, new RemainingActionPoints { Value = 1 });
            f.RunAction();
            f.FinishStage();
            var end = f.World.CreateSystem<TurnEndSystem>();
            end.Update(f.World.Unmanaged);
            f.Events.Update(f.World.Unmanaged);
            var duration = f.World.CreateSystem<EffectDurationProcessingSystem>();
            duration.Update(f.World.Unmanaged);
            f.Phases.Update(f.World.Unmanaged);
            Assert.That(
                f.EM.GetComponentData<ActiveTurnPlayer>(f.Battle).Player,
                Is.EqualTo(f.Player)
            );
            Assert.That(
                f.EM.GetComponentData<BattleState>(f.Battle).Phase,
                Is.EqualTo(BattlePhase.TurnStart)
            );
            Assert.That(f.EM.GetComponentData<TurnCounter>(f.Battle).CurrentTurn, Is.EqualTo(2));
            Assert.That(
                f.EM.GetComponentData<RemainingActionPoints>(f.Opponent).Value,
                Is.EqualTo(1),
                "Skipped start does not refresh resources."
            );
            Assert.That(f.EM.GetBuffer<PendingStageSkip>(f.Battle).Length, Is.Zero);
            Assert.That(f.EM.GetBuffer<BattleEvent>(f.Battle).Length, Is.Zero);
        }

        [Test]
        public void DeadActorAtHandoffOrDisabledManualActorCannotOpenExtraAction()
        {
            using var f = new SchedulerTestFixture(false);
            f.Commit();
            Entity parent = f.Context;
            f.Apply(f.Control(ActionControlKind.ExtraAction, f.Karathos));
            Assert.That(f.EM.GetBuffer<PendingContextTransition>(f.Battle).Length, Is.Zero);
            f.Apply(f.Control(ActionControlKind.ExtraAction, f.Mei));
            f.EM.SetComponentData(
                f.Mei,
                new Archeus.Battle.Components.Stats.CurrentHealth { Value = 0 }
            );
            f.Phases.Update(f.World.Unmanaged);
            Assert.That(f.Context, Is.EqualTo(parent));
            Assert.That(f.EM.GetBuffer<PendingContextTransition>(f.Battle).Length, Is.Zero);
            Assert.That(
                f.EM.GetComponentData<AttackingStageExecutionContext>(parent).ExecutionStatus,
                Is.EqualTo(AttackingStageExecutionStatus.Active)
            );
        }

        [Test]
        public void ContextDepthLimitIsExplicitAndDoesNotSuspendParent()
        {
            using var f = new SchedulerTestFixture(false);
            f.Commit();
            var context = f.EM.GetComponentData<AttackingStageExecutionContext>(f.Context);
            context.Depth = ActionQueueAlgorithms.MaximumContextDepth;
            f.EM.SetComponentData(f.Context, context);
            f.Apply(f.Control(ActionControlKind.ExtraAction, f.Mei));
            Assert.That(
                f.EM.GetComponentData<ActionSchedulerState>(f.Battle).Failure,
                Is.EqualTo(SchedulerFailure.ContextDepth)
            );
            Assert.That(
                f.EM.GetComponentData<AttackingStageExecutionContext>(f.Context).ExecutionStatus,
                Is.EqualTo(AttackingStageExecutionStatus.Active)
            );
            Assert.That(f.EM.GetBuffer<PendingContextTransition>(f.Battle).Length, Is.Zero);
        }
    }
}
