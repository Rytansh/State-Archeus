using System.Linq;
using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Scheduling;
using Archeus.Battle.Snapshots;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Testing.Battle.Scheduling
{
    public sealed class ActionSchedulerIntegrationTests
    {
        [Test]
        public void RealKarathosPassiveInterruptsTomaBeforeEnergyAndInheritsSourceWithoutChildContext()
        {
            using var f = new SchedulerTestFixture();
            uint mei = f.Plan(f.Mei, f.Enemy);
            uint toma = f.Plan(f.Toma, f.Enemy, CharacterActionType.SuperchargedAttack);
            uint harun = f.Plan(f.Harun, f.Enemy);
            uint context = f.ContextID;
            CollectionAssert.AreEqual(
                new[] { mei, toma, harun },
                f.Projection.Contexts.Single().Pending.Select(x => x.QueueActionID)
            );
            f.Commit();
            uint epoch = f.EM.GetComponentData<BattlePhaseEpoch>(f.Battle).Value;
            f.RunAction();
            Assert.That(f.HP(f.Enemy), Is.EqualTo(990));
            CollectionAssert.AreEqual(
                new[] { toma, harun },
                f.Projection.Contexts.Single().Pending.Select(x => x.QueueActionID)
            );
            f.RunAction();
            var changed = f.Projection;
            Assert.That(changed.ActiveContextID, Is.EqualTo(context));
            Assert.That(changed.Contexts.Length, Is.EqualTo(1));
            Assert.That(changed.Contexts[0].Pending[0].SourceRuntimeID, Is.EqualTo(103));
            Assert.That(
                changed.Contexts[0].Pending[0].Origin,
                Is.EqualTo(ActionOrigin.InterruptReplacement)
            );
            Assert.That(changed.Contexts[0].Pending[0].CausingExecutionID, Is.EqualTo(2));
            Assert.That(changed.Contexts[0].Pending[0].InheritedSourceRuntimeID, Is.EqualTo(101));
            Assert.That(changed.Contexts[0].Pending[1].QueueActionID, Is.EqualTo(harun));
            Assert.That(f.HP(f.Enemy), Is.EqualTo(990));
            Assert.That(f.EM.GetComponentData<HeldSuperchargedEnergy>(f.Toma).Value, Is.EqualTo(5));
            Assert.That(
                f.EM.GetBuffer<BehaviourRuntimeState>(f.Toma)[0].Memory.Length,
                Is.Zero,
                "Performed hook must not run for an interrupted attempt."
            );
            f.RunAction();
            f.RunAction();
            Assert.That(
                f.HP(f.Enemy),
                Is.EqualTo(880),
                "Karathos uses Toma's inherited Attack / 10 as his VM multiplier."
            );
            Assert.That(f.EM.GetComponentData<HeldSuperchargedEnergy>(f.Karathos).Value, Is.Zero);
            Assert.That(f.EM.GetComponentData<BattlePhaseEpoch>(f.Battle).Value, Is.EqualTo(epoch));
            Assert.That(
                f.EM.GetComponentData<BattleState>(f.Battle).Phase,
                Is.EqualTo(BattlePhase.Attacking)
            );
            Assert.That(
                f.EM.GetComponentData<AttackingStageExecutionInstanceCounter>(f.Battle).NextID,
                Is.EqualTo(2)
            );
            var outcomes = f
                .EM.GetBuffer<ActionOutcomeRecord>(f.Battle)
                .ToNativeArray(Allocator.Temp);
            using (outcomes)
            {
                CollectionAssert.AreEqual(
                    new[]
                    {
                        ActionOutcome.Completed,
                        ActionOutcome.Interrupted,
                        ActionOutcome.Completed,
                        ActionOutcome.Completed,
                    },
                    outcomes.Select(x => x.Outcome)
                );
                Assert.That(outcomes[2].CausingExecutionID, Is.EqualTo(outcomes[1].ExecutionID));
            }
            using var facts = f
                .EM.GetBuffer<PresentationFact>(f.Battle)
                .ToNativeArray(Allocator.Temp);
            var damage = facts
                .Where(x => x.FactType == PresentationFactType.DamageApplied)
                .ToArray();
            CollectionAssert.AreEqual(
                new uint[] { 100, 103, 102 },
                damage.Select(x => x.FactMetadata.SourceRuntimeID)
            );
            Assert.That(
                facts.Count(x => x.FactType == PresentationFactType.ActionCompleted),
                Is.EqualTo(3)
            );
            Assert.That(
                facts.Count(x => x.FactType == PresentationFactType.ActionInterrupted),
                Is.EqualTo(1)
            );
            Assert.That(
                facts.Select(x => x.FactMetadata.Sequence).Distinct().Count(),
                Is.EqualTo(facts.Length)
            );
            var transitions = facts
                .Where(x => x.FactType == PresentationFactType.SchedulerTransition)
                .ToArray();
            int interrupted = System.Array.FindIndex(
                transitions,
                x => x.FactPayload.Scheduler.Kind == SchedulerFactKind.Interrupted
            );
            Assert.That(
                transitions[interrupted + 1].FactPayload.Scheduler.Kind,
                Is.EqualTo(SchedulerFactKind.Inserted)
            );
            Assert.That(
                transitions[interrupted + 1].FactPayload.Scheduler.SourceRuntimeID,
                Is.EqualTo(103)
            );
        }

        [Test]
        public void ReplacementWithoutEnergyIsRejectedAndOriginalAttackProceeds()
        {
            using var f = new SchedulerTestFixture();
            f.EM.SetComponentData(f.Karathos, new HeldSuperchargedEnergy { Value = 4 });
            f.Plan(f.Toma, f.Enemy, CharacterActionType.SuperchargedAttack);
            f.Commit();
            f.RunAction();
            Assert.That(
                f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle)[0].Outcome,
                Is.EqualTo(ActionOutcome.Completed)
            );
            Assert.That(f.HP(f.Enemy), Is.EqualTo(960));
            Assert.That(f.EM.GetComponentData<HeldSuperchargedEnergy>(f.Toma).Value, Is.Zero);
            Assert.That(
                f.EM.GetComponentData<HeldSuperchargedEnergy>(f.Karathos).Value,
                Is.EqualTo(4)
            );
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.Zero);
        }

        [Test]
        public void PerformedHookCannotRetroactivelyInterruptAnAuthorizedAttack()
        {
            using var f = new SchedulerTestFixture(reactionEvent: BattleEventType.ActionPerformed);
            f.Plan(f.Toma, f.Enemy, CharacterActionType.SuperchargedAttack);
            f.Commit();
            f.RunAction();
            Assert.That(
                f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle)[0].Outcome,
                Is.EqualTo(ActionOutcome.Completed)
            );
            Assert.That(f.HP(f.Enemy), Is.EqualTo(960));
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.Zero);
            Assert.That(f.EM.GetBuffer<ActionControlResult>(f.Battle)[0].Accepted, Is.False);
        }

        [Test]
        public void QueueShrinksOnceAndNextDispatchWaitsForAllEventWork()
        {
            using var f = new SchedulerTestFixture(false);
            uint first = f.Plan(f.Mei, f.Enemy);
            uint next = f.Plan(f.Harun, f.Enemy);
            f.Commit();
            f.Dispatch.Update(f.World.Unmanaged);
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.EqualTo(1));
            Assert.That(f.Projection.HasExecuting, Is.True);
            Assert.That(f.Projection.Executing.QueueActionID, Is.EqualTo(first));
            f.Dispatch.Update(f.World.Unmanaged);
            Assert.That(
                f.EM.GetComponentData<BattleActionExecutionCounter>(f.Battle).NextID,
                Is.EqualTo(2)
            );
            f.Events.Update(f.World.Unmanaged);
            Assert.That(ActionQueueService.IsSettled(f.EM, f.Battle), Is.True);
            f.RunAction();
            f.Events.Update(f.World.Unmanaged);
            Assert.That(f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle).Length, Is.EqualTo(2));
            Assert.That(
                f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle)[1].QueueActionID,
                Is.EqualTo(next)
            );
            Assert.That(f.HP(f.Enemy), Is.EqualTo(960));
            f.FinishStage();
            Assert.That(
                f.EM.GetComponentData<BattleState>(f.Battle).Phase,
                Is.EqualTo(BattlePhase.TurnEnd)
            );
        }

        [Test]
        public void CancellationSuppressesOnlyItsAttemptAndPreservesUnrelatedDamage()
        {
            using var f = new SchedulerTestFixture(true, ActionControlKind.CancelCurrent);
            f.Plan(f.Toma, f.Enemy, CharacterActionType.SuperchargedAttack);
            f.Commit();
            f.Dispatch.Update(f.World.Unmanaged);
            f.EM.GetBuffer<BattleEvent>(f.Battle)
                .Add(
                    new BattleEvent
                    {
                        Type = BattleEventType.DamageRequested,
                        Scope = BattleEventScope.Targeted,
                        Source = f.Mei,
                        Target = f.Harun,
                        Payload = new EventPayload
                        {
                            Damage = new DamagePayload { AttackMultiplier = 1 },
                        },
                    }
                );
            f.Events.Update(f.World.Unmanaged);
            Assert.That(f.HP(f.Enemy), Is.EqualTo(1000));
            Assert.That(f.HP(f.Harun), Is.EqualTo(990));
            Assert.That(
                f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle)[0].Outcome,
                Is.EqualTo(ActionOutcome.Cancelled)
            );
            Assert.That(f.EM.GetComponentData<HeldSuperchargedEnergy>(f.Toma).Value, Is.EqualTo(5));
        }

        [Test]
        public void CompetingRealPassivesChooseStableOwnerOrder()
        {
            using var f = new SchedulerTestFixture();
            // Allocate/register this lower runtime ID after Karathos: entity/chunk order must not decide.
            f.EM.AddBuffer<BehaviourReference>(f.Mei)
                .Add(new BehaviourReference { BehaviourIndex = 0 });
            f.EM.GetBuffer<BehaviourRuntimeState>(f.Mei).Add(default);
            f.Plan(f.Toma, f.Enemy, CharacterActionType.SuperchargedAttack);
            f.Commit();
            f.RunAction();
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.EqualTo(1));
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context)[0].Source, Is.EqualTo(f.Mei));
            Assert.That(
                f.EM.GetBuffer<ActionControlResult>(f.Battle).Count(x => x.Accepted),
                Is.EqualTo(1)
            );
        }

        [Test]
        public void DirectVMProgramWaitsForMultiHitContinuationsAndPreservesHitGroups()
        {
            using var f = new SchedulerTestFixture(false);
            f.EM.AddBuffer<ActionProgramBinding>(f.Mei)
                .Add(
                    new ActionProgramBinding
                    {
                        Type = CharacterActionType.NormalAttack,
                        DefinitionID = 9,
                        ProgramIndex = 3,
                        TargetMode = ActionTargetMode.SingleEnemy,
                    }
                );
            f.Plan(f.Mei, f.Enemy, definitionID: 9);
            f.Plan(f.Harun, f.Enemy);
            f.Commit();
            f.RunAction();
            Assert.That(f.HP(f.Enemy), Is.EqualTo(980));
            Assert.That(ActionQueueService.IsSettled(f.EM, f.Battle), Is.True);
            Assert.That(f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle).Length, Is.EqualTo(1));
            using var facts = f
                .EM.GetBuffer<PresentationFact>(f.Battle)
                .ToNativeArray(Allocator.Temp);
            var hits = facts.Where(x => x.FactType == PresentationFactType.DamageApplied).ToArray();
            CollectionAssert.AreEqual(
                new ushort[] { 0, 1 },
                hits.Select(x => x.FactMetadata.HitIndex)
            );
            CollectionAssert.AreEqual(
                new ushort[] { 0, 1 },
                hits.Select(x => x.FactMetadata.ActionResultIndex)
            );
            Assert.That(hits.All(x => x.FactMetadata.ActionExecutionID == 1), Is.True);
            Assert.That(hits.All(x => x.FactMetadata.ActionDefinitionID == 9), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidRuntimeTargetOrUnsupportedActionFailsWithoutSuccessOrCosts(
            bool unsupported
        )
        {
            using var f = new SchedulerTestFixture(false);
            f.Plan(f.Toma, f.Enemy, CharacterActionType.SuperchargedAttack);
            f.Commit();
            if (unsupported)
            {
                var queue = f.EM.GetBuffer<PlannedAction>(f.Context);
                var action = queue[0];
                action.CharacterAction = CharacterActionType.Transformation;
                queue[0] = action;
            }
            else
                f.EM.SetComponentData(f.Enemy, new CurrentHealth { Value = 0 });
            f.RunAction();
            Assert.That(
                f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle)[0].Outcome,
                Is.EqualTo(ActionOutcome.Failed)
            );
            Assert.That(f.EM.GetComponentData<HeldSuperchargedEnergy>(f.Toma).Value, Is.EqualTo(5));
            using var facts = f
                .EM.GetBuffer<PresentationFact>(f.Battle)
                .ToNativeArray(Allocator.Temp);
            Assert.That(
                facts.Count(x => x.FactType == PresentationFactType.ActionCompleted),
                Is.Zero
            );
            Assert.That(
                facts.Count(x => x.FactType == PresentationFactType.ActionFailed),
                Is.EqualTo(1)
            );
        }

        [Test]
        public void SameWorldBattlesWithOverlappingRuntimeIDsRemainIsolated()
        {
            using var world = new World("Simultaneous scheduler battles");
            using var a = new SchedulerTestFixture(true, sharedWorld: world, battleID: 42);
            using var b = new SchedulerTestFixture(false, sharedWorld: world, battleID: 43);
            a.Plan(a.Toma, a.Enemy, CharacterActionType.SuperchargedAttack);
            b.Plan(b.Toma, b.Enemy, CharacterActionType.SuperchargedAttack);
            a.Commit();
            b.Commit();
            a.RunAction();
            Assert.That(
                a.EM.GetBuffer<ActionOutcomeRecord>(a.Battle)[0].Outcome,
                Is.EqualTo(ActionOutcome.Interrupted)
            );
            Assert.That(
                b.EM.GetBuffer<ActionOutcomeRecord>(b.Battle)[0].Outcome,
                Is.EqualTo(ActionOutcome.Completed)
            );
            Assert.That(a.HP(a.Enemy), Is.EqualTo(1000));
            Assert.That(b.HP(b.Enemy), Is.EqualTo(960));
            Assert.That(a.EM.GetComponentData<HeldSuperchargedEnergy>(a.Toma).Value, Is.EqualTo(5));
            Assert.That(b.EM.GetComponentData<HeldSuperchargedEnergy>(b.Toma).Value, Is.Zero);
        }

        [Test]
        public void RunawayActionProgramHaltsExplicitlyWithoutCompletingOrDispatchingNextAction()
        {
            using var f = new SchedulerTestFixture(false);
            f.EM.AddBuffer<ActionProgramBinding>(f.Mei)
                .Add(
                    new ActionProgramBinding
                    {
                        Type = CharacterActionType.NormalAttack,
                        DefinitionID = 9,
                        ProgramIndex = 4,
                        TargetMode = ActionTargetMode.None,
                    }
                );
            f.Plan(f.Mei, Entity.Null, definitionID: 9);
            f.Plan(f.Harun, f.Enemy);
            f.Commit();
            f.RunAction();
            Assert.That(
                f.EM.GetComponentData<ActionSchedulerState>(f.Battle).Failure,
                Is.EqualTo(SchedulerFailure.EventEngineUnsettled)
            );
            Assert.That(f.EM.GetBuffer<ActionOutcomeRecord>(f.Battle).Length, Is.Zero);
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.EqualTo(1));
            f.RunAction();
            Assert.That(f.HP(f.Enemy), Is.EqualTo(1000));
            Assert.That(
                f.EM.GetComponentData<BattleActionExecutionCounter>(f.Battle).NextID,
                Is.EqualTo(2)
            );
        }

        [Test]
        public void SnapshotReplayReproducesQueueIDsFactsVMAndFinalState()
        {
            using var f = new SchedulerTestFixture();
            f.Plan(f.Mei, f.Enemy);
            f.Plan(f.Toma, f.Enemy, CharacterActionType.SuperchargedAttack);
            f.Plan(f.Harun, f.Enemy);
            var snapshot = BattleStateSnapshot.Capture(f.EM, f.Battle);
            string expected = null;
            for (int replay = 0; replay < 3; replay++)
            {
                f.Commit();
                for (int i = 0; i < 4; i++)
                    f.RunAction();
                string hash = snapshot.ComputeCurrentHash(f.EM);
                if (expected == null)
                    expected = hash;
                Assert.That(hash, Is.EqualTo(expected));
                snapshot.Restore(f.EM);
                f.Snapshot.Open(); // Generic restore excludes the operational input fence; reopen it before the next commit.
            }
        }
    }
}
