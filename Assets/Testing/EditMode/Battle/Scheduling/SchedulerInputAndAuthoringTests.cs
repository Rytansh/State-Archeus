using System.Collections.Generic;
using Archeus.Battle.Buffers.Input;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Data.VM;
using Archeus.Battle.Scheduling;
using Archeus.Battle.Snapshots;
using Archeus.Battle.Systems.Input;
using Archeus.Game.Input;
using NUnit.Framework;
using Unity.Entities;

namespace Archeus.Testing.Battle.Scheduling
{
    public sealed class SchedulerInputAndAuthoringTests
    {
        [Test]
        public void GatewayPlansAndReordersWhileStaleContextOrEpochCannotMutate()
        {
            using var f = new SchedulerTestFixture(false);
            Entity inbox = f.EM.CreateEntity();
            f.EM.AddComponent<BattleInputInboxTag>(inbox);
            f.EM.AddBuffer<BattleInputCommand>(inbox);
            var gateway = new BattleInputGateway(f.EM, inbox);
            var routing = f.World.CreateSystem<BattleInputRoutingSystem>();
            gateway.RequestQueueAction(42, BattleSide.SideA, 100, 200);
            gateway.RequestQueueAction(42, BattleSide.SideA, 102, 200);
            routing.Update(f.World.Unmanaged);
            uint first = f.EM.GetBuffer<PlannedAction>(f.Context)[0].QueueActionID,
                last = f.EM.GetBuffer<PlannedAction>(f.Context)[1].QueueActionID;
            gateway.RequestMoveAction(42, BattleSide.SideA, last, 0);
            routing.Update(f.World.Unmanaged);
            Assert.That(
                f.EM.GetBuffer<PlannedAction>(f.Context)[0].QueueActionID,
                Is.EqualTo(last)
            );
            gateway.RequestRemoveAction(42, BattleSide.SideA, first);
            var commands = f.EM.GetBuffer<BattleInputCommand>(inbox);
            var stale = commands[0];
            stale.ContextID++;
            commands[0] = stale;
            routing.Update(f.World.Unmanaged);
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.EqualTo(2));
            gateway.RequestRemoveAction(42, BattleSide.SideA, first);
            commands = f.EM.GetBuffer<BattleInputCommand>(inbox);
            stale = commands[0];
            stale.PhaseEpoch++;
            commands[0] = stale;
            routing.Update(f.World.Unmanaged);
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.EqualTo(2));
            gateway.RequestRemoveAction(42, BattleSide.SideA, first);
            routing.Update(f.World.Unmanaged);
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.EqualTo(1));
        }

        [Test]
        public void PlanningRevertRestoresQueueStableIDCounterAndFencesOldQueueInput()
        {
            using var f = new SchedulerTestFixture(false);
            ulong revision = f.Snapshot.Open();
            uint first = f.Plan(f.Mei, f.Enemy);
            Entity inbox = f.EM.CreateEntity();
            f.EM.AddComponent<BattleInputInboxTag>(inbox);
            f.EM.AddBuffer<BattleInputCommand>(inbox);
            var gateway = new BattleInputGateway(f.EM, inbox);
            gateway.RequestQueueAction(42, BattleSide.SideA, 102, 200);
            Assert.That(f.Snapshot.Snapshots.TryRevert(f.Player, revision), Is.True);
            f.World.CreateSystem<BattleInputRoutingSystem>().Update(f.World.Unmanaged);
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.Zero);
            Assert.That(f.Plan(f.Mei, f.Enemy), Is.EqualTo(first));
        }

        [Test]
        public void ManualSelectionCannotRemoveAutomaticActionsOrUseOpponentActor()
        {
            using var f = new SchedulerTestFixture(false);
            Entity inbox = f.EM.CreateEntity();
            f.EM.AddComponent<BattleInputInboxTag>(inbox);
            f.EM.AddBuffer<BattleInputCommand>(inbox);
            var gateway = new BattleInputGateway(f.EM, inbox);
            var routing = f.World.CreateSystem<BattleInputRoutingSystem>();
            f.Apply(f.Control(ActionControlKind.Append, f.Mei));
            uint automatic = f.EM.GetBuffer<PlannedAction>(f.Context)[0].QueueActionID;
            gateway.RequestRemoveAction(42, BattleSide.SideA, automatic);
            gateway.RequestQueueAction(42, BattleSide.SideA, 200, 100);
            routing.Update(f.World.Unmanaged);
            Assert.That(f.EM.GetBuffer<PlannedAction>(f.Context).Length, Is.EqualTo(1));
        }

        [Test]
        public void AbilityCompilerPreservesTypedControlAndInheritedStatOperands()
        {
            var code = AbilityProgramCompiler.Compile(
                "RequestActionControl ReplaceCurrent SuperchargedAttack 77\nPushInheritedStat 0\nEnd",
                new Dictionary<uint, int>()
            );
            Assert.That(code[0].Opcode, Is.EqualTo(AbilityOpcode.RequestActionControl));
            Assert.That(code[0].A, Is.EqualTo((int)ActionControlKind.ReplaceCurrent));
            Assert.That(code[0].B, Is.EqualTo((int)CharacterActionType.SuperchargedAttack));
            Assert.That(code[0].C, Is.EqualTo(77));
            Assert.That(code[0].Flags, Is.EqualTo(AbilityInstructionFlags.None));
            Assert.That(code[1].Opcode, Is.EqualTo(AbilityOpcode.PushInheritedStat));
        }
    }
}
