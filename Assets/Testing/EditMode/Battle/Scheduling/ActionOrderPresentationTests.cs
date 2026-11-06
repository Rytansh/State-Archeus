using System.Linq;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Scheduling;
using Archeus.Battle.Systems.Presentation;
using Archeus.Game.Bootstrap;
using NUnit.Framework;
using Unity.Entities;

namespace Archeus.Testing.Battle.Scheduling
{
    public sealed class ActionOrderPresentationTests
    {
        private static void Connect(World world, BattlePresentationBridge bridge)
        {
            Entity reference = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentObject(
                reference,
                new BattlePresentationBridgeReference { Bridge = bridge }
            );
        }

        [Test]
        public void InitialIncrementalAndRecoveryExportsReconcileAuthoritativeOrder()
        {
            using var f = new SchedulerTestFixture(false);
            using var presentation = new World("Action order projection import");
            _ = new BattlePresentationWorld(presentation);
            var bridge = new BattlePresentationBridge();
            Connect(f.World, bridge);
            Connect(presentation, bridge);
            f.EM.AddComponentData(f.Battle, new PresentationStateRevision());
            f.Plan(f.Mei, f.Enemy);
            f.World.GetOrCreateSystemManaged<PresentationSnapshotExportSystem>().Update();
            var imports = presentation.GetOrCreateSystemManaged<PresentationSnapshotImportSystem>();
            imports.Update();
            var mirror = ActionOrderMirrorAccess.Get(presentation.EntityManager);
            Assert.That(mirror.State.Contexts.Single().Pending.Length, Is.EqualTo(1));
            f.Apply(f.Control(ActionControlKind.Append, f.Harun));
            f.World.GetOrCreateSystemManaged<ActionOrderExportSystem>().Update();
            presentation.GetOrCreateSystemManaged<PresentationUpdateImportSystem>().Update();
            Assert.That(mirror.State.Contexts.Single().Pending.Length, Is.EqualTo(2));
            // A recovery publication must repair even a completely missing local queue projection.
            mirror.State = new ActionOrderSnapshot { BattleID = 42 };
            bridge.RequestStateRecovery(42);
            f.World.GetOrCreateSystemManaged<PresentationStateRecoverySystem>().Update();
            imports.Update();
            Assert.That(
                ActionOrderPublication.Fingerprint(mirror.State),
                Is.EqualTo(ActionOrderPublication.Fingerprint(f.Projection))
            );
        }

        [Test]
        public void IntermediateTransitionsAreNotLostAndSchedulerFactsNeverEnterAnimationPackets()
        {
            using var f = new SchedulerTestFixture();
            using var presentation = new World("Scheduler fact import");
            _ = new BattlePresentationWorld(presentation);
            var bridge = new BattlePresentationBridge();
            Connect(f.World, bridge);
            Connect(presentation, bridge);
            f.Plan(
                f.Toma,
                f.Enemy,
                Archeus.Battle.Data.Actions.CharacterActionType.SuperchargedAttack
            );
            f.Commit();
            f.RunAction();
            f.World.GetOrCreateSystemManaged<PresentationFactExportSystem>().Update();
            presentation.GetOrCreateSystemManaged<PresentationFactImportSystem>().Update();
            presentation.GetOrCreateSystemManaged<ActionOrderFactImportSystem>().Update();
            var mirror = ActionOrderMirrorAccess.Get(presentation.EntityManager);
            var transitions = mirror.Transitions.ToArray();
            int index = System.Array.FindIndex(
                transitions,
                x => x.Kind == SchedulerFactKind.Interrupted
            );
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(transitions[index + 1].Kind, Is.EqualTo(SchedulerFactKind.Inserted));
            Assert.That(transitions[index + 2].Kind, Is.EqualTo(SchedulerFactKind.AttemptSettled));
            var assembly = presentation.CreateSystem<BattlePresentationAssemblySystem>();
            assembly.Update(presentation.Unmanaged);
            using var packets = presentation.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PresentationActionPacket>()
            );
            Assert.That(
                packets.CalculateEntityCount(),
                Is.Zero,
                "Interrupted attempt packets must be removed, never sealed as success."
            );
            Assert.That(
                f.EM.GetBuffer<PlannedAction>(f.Context).Length,
                Is.EqualTo(1),
                "UI import must not advance simulation."
            );
        }

        [Test]
        public void PlanningReversionClearsAbandonedTransitionsAndRecoveryRestoresQueue()
        {
            using var f = new SchedulerTestFixture(false);
            using var presentation = new World("Reverted order mirror");
            _ = new BattlePresentationWorld(presentation);
            var bridge = new BattlePresentationBridge();
            Connect(f.World, bridge);
            Connect(presentation, bridge);
            f.EM.AddComponentData(f.Battle, new PresentationStateRevision());
            f.World.GetOrCreateSystemManaged<PresentationSnapshotExportSystem>().Update();
            var imports = presentation.GetOrCreateSystemManaged<PresentationSnapshotImportSystem>();
            imports.Update();
            ulong revision = f.Snapshot.Open();
            f.Plan(f.Mei, f.Enemy);
            f.World.GetOrCreateSystemManaged<ActionOrderExportSystem>().Update();
            presentation.GetOrCreateSystemManaged<PresentationUpdateImportSystem>().Update();
            f.World.GetOrCreateSystemManaged<PresentationFactExportSystem>().Update();
            presentation.GetOrCreateSystemManaged<PresentationFactImportSystem>().Update();
            presentation.GetOrCreateSystemManaged<ActionOrderFactImportSystem>().Update();
            var mirror = ActionOrderMirrorAccess.Get(presentation.EntityManager);
            Assert.That(mirror.State.Contexts.Single().Pending.Length, Is.EqualTo(1));
            Assert.That(f.Snapshot.Snapshots.TryRevert(f.Player, revision), Is.True);
            presentation.GetOrCreateSystemManaged<PresentationStageReversionSystem>().Update();
            Assert.That(mirror.State, Is.Null);
            Assert.That(mirror.Transitions.Count, Is.Zero);
            f.World.GetOrCreateSystemManaged<PresentationStateRecoverySystem>().Update();
            imports.Update();
            Assert.That(mirror.State.Contexts.Single().Pending.Length, Is.Zero);
        }

        [Test]
        public void ContextProjectionIncludesSuspendedParentBudgetAndActiveChildRestriction()
        {
            using var f = new SchedulerTestFixture(false);
            f.Plan(f.Mei, f.Enemy);
            f.Commit();
            Entity parent = f.Context;
            f.Apply(f.Control(ActionControlKind.ExtraAction, f.Harun));
            f.Phases.Update(f.World.Unmanaged);
            var state = f.Projection;
            Assert.That(state.Contexts.Length, Is.EqualTo(2));
            var parentView = state.Contexts.Single(x => x.ContextID != state.ActiveContextID);
            var child = state.Contexts.Single(x => x.ContextID == state.ActiveContextID);
            Assert.That(parentView.Status, Is.EqualTo(AttackingStageExecutionStatus.Suspended));
            Assert.That(parentView.RemainingAP, Is.EqualTo(3));
            Assert.That(parentView.Pending.Length, Is.EqualTo(1));
            Assert.That(child.ParentID, Is.EqualTo(parentView.ContextID));
            Assert.That(child.RestrictedActor, Is.EqualTo(102));
            Assert.That(child.RemainingAP, Is.EqualTo(1));
        }
    }
}
