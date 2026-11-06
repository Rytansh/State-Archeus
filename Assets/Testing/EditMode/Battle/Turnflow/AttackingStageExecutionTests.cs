using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace Archeus.Testing.Battle.Turnflow
{
    public class AttackingStageExecutionTests
    {
        private World world;
        private EntityManager em;
        private Entity battle;
        private Entity playerA;
        private Entity playerB;
        private Entity characterB;

        [SetUp]
        public void SetUp()
        {
            world = new World("Execution Context Test World");
            em = world.EntityManager;

            battle = em.CreateEntity();
            em.AddComponent<BattleTag>(battle);
            em.AddComponentData(battle, new BattleState { Phase = BattlePhase.Setup });
            em.AddComponentData(
                battle,
                new AttackingStageExecutionInstance { Context = Entity.Null }
            );
            em.AddComponentData(battle, new AttackingStageExecutionInstanceCounter { NextID = 1 });
            em.AddComponentData(battle, new BattlePhaseEpoch { Value = 0 });
            playerA = em.CreateEntity();
            playerB = em.CreateEntity();
            characterB = em.CreateEntity();
            foreach (Entity player in new[] { playerA, playerB })
            {
                em.AddComponent<PlayerTag>(player);
                em.AddComponentData(player, new OwnedBattle { Battle = battle });
                em.AddComponentData(player, new RemainingActionPoints { Value = 4 });
                em.AddBuffer<FieldCard>(player);
            }
            em.AddComponentData(playerA, new BattleSideAssignment { Side = BattleSide.SideA });
            em.AddComponentData(playerB, new BattleSideAssignment { Side = BattleSide.SideB });
            em.AddComponentData(characterB, new BattleSideAssignment { Side = BattleSide.SideB });
            em.AddComponentData(characterB, new OwnedBattle { Battle = battle });
            em.AddComponentData(characterB, new CardOwner { Player = playerB });
            em.AddComponentData(characterB, new CurrentHealth { Value = 100 });
            em.GetBuffer<FieldCard>(playerB).Add(new FieldCard { Card = characterB });
        }

        [TearDown]
        public void TearDown() => world.Dispose();

        private Entity CreateRoot(Entity owner)
        {
            bool created = AttackingStageExecutionContextFactory.TryCreate(
                em,
                battle,
                owner,
                AttackingStageExecutionType.NormalTurn,
                Entity.Null,
                Entity.Null,
                out Entity result
            );
            Assert.That(created, Is.True);
            return result;
        }

        private Entity CreateExtraTurn(Entity parent)
        {
            bool created = AttackingStageExecutionContextFactory.TryCreate(
                em,
                battle,
                playerB,
                AttackingStageExecutionType.ExtraTurn,
                parent,
                Entity.Null,
                out Entity result
            );
            Assert.That(created, Is.True);
            return result;
        }

        private Entity CreateExtraAction(Entity parent)
        {
            bool created = AttackingStageExecutionContextFactory.TryCreate(
                em,
                battle,
                playerB,
                AttackingStageExecutionType.ExtraAction,
                parent,
                characterB,
                out Entity result
            );
            Assert.That(created, Is.True);
            return result;
        }

        private void SetPhaseForTest(BattlePhase phase)
        {
            em.SetComponentData(battle, new BattleState { Phase = phase });
            var epoch = em.GetComponentData<BattlePhaseEpoch>(battle);
            epoch.Value++;
            em.SetComponentData(battle, epoch);
        }

        private Entity Active =>
            em.GetComponentData<AttackingStageExecutionInstance>(battle).Context;

        private AttackingStageExecutionContext Context(Entity e) =>
            em.GetComponentData<AttackingStageExecutionContext>(e);

        [Test]
        public void FactoryCreatesDistinctContextsWithEmptyActionPlans()
        {
            Entity root = CreateRoot(playerA);
            Entity child = CreateExtraTurn(root);
            Assert.That(Context(root).ContextID, Is.EqualTo(1U));
            Assert.That(Context(child).ContextID, Is.EqualTo(2U));
            Assert.That(Context(child).ParentContext, Is.EqualTo(root));
            Assert.That(em.GetBuffer<PlannedAction>(root).Length, Is.Zero);
            Assert.That(em.GetBuffer<PlannedAction>(child).Length, Is.Zero);
            Assert.That(Active, Is.EqualTo(Entity.Null));
            Assert.That(
                em.GetComponentData<AttackingStageExecutionInstanceCounter>(battle).NextID,
                Is.EqualTo(3U)
            );
        }

        [Test]
        public void RootActivationMovesSetupToTurnStartOnce()
        {
            Entity root = CreateRoot(playerA);
            Assert.That(AttackingStageExecutionManager.TryActivateRoot(em, battle, root), Is.True);
            Assert.That(Active, Is.EqualTo(root));
            Assert.That(
                Context(root).ExecutionStatus,
                Is.EqualTo(AttackingStageExecutionStatus.Active)
            );
            Assert.That(
                em.GetComponentData<BattleState>(battle).Phase,
                Is.EqualTo(BattlePhase.TurnStart)
            );
            Assert.That(em.GetComponentData<BattlePhaseEpoch>(battle).Value, Is.EqualTo(1U));
            Assert.That(AttackingStageExecutionManager.TryActivateRoot(em, battle, root), Is.False);
            Assert.That(em.GetComponentData<BattlePhaseEpoch>(battle).Value, Is.EqualTo(1U));
        }

        [Test]
        public void ChildPreservesParentsPendingActionPlanAndAP()
        {
            Entity root = CreateRoot(playerA);
            Assert.That(AttackingStageExecutionManager.TryActivateRoot(em, battle, root), Is.True);
            var plan = em.GetBuffer<PlannedAction>(root);
            for (int i = 0; i < 3; i++)
                plan.Add(
                    new PlannedAction
                    {
                        ActionType = PlannedActionType.CharacterAction,
                        SubmittedByPlayer = playerA,
                        Source = playerA,
                    }
                );
            var data = Context(root);
            data.RemainingActionPoints = 2;
            em.SetComponentData(playerA, new RemainingActionPoints { Value = 2 });
            em.SetComponentData(root, data);

            SetPhaseForTest(BattlePhase.Attacking);
            Entity child = CreateExtraTurn(root);
            Assert.That(AttackingStageExecutionManager.TryBeginChild(em, battle, child), Is.True);
            Assert.That(Active, Is.EqualTo(child));
            Assert.That(
                Context(root).ExecutionStatus,
                Is.EqualTo(AttackingStageExecutionStatus.Suspended)
            );
            Assert.That(
                em.GetComponentData<BattleState>(battle).Phase,
                Is.EqualTo(BattlePhase.Drawing)
            );

            // The child has completed Drawing + Planning and its Attacking work.
            SetPhaseForTest(BattlePhase.Attacking);
            Assert.That(
                AttackingStageExecutionManager.TryCompleteChildAndResume(em, battle),
                Is.True
            );
            Assert.That(Active, Is.EqualTo(root));
            Assert.That(
                Context(child).ExecutionStatus,
                Is.EqualTo(AttackingStageExecutionStatus.Completed)
            );
            Assert.That(
                Context(root).ExecutionStatus,
                Is.EqualTo(AttackingStageExecutionStatus.Active)
            );
            Assert.That(em.GetBuffer<PlannedAction>(root).Length, Is.EqualTo(3));
            Assert.That(em.GetComponentData<RemainingActionPoints>(playerA).Value, Is.EqualTo(2));
            Assert.That(
                em.GetComponentData<BattleState>(battle).Phase,
                Is.EqualTo(BattlePhase.Attacking)
            );
        }

        [Test]
        public void NestedExtraActionRestoresImmediateParentBeforeRoot()
        {
            Entity root = CreateRoot(playerA);
            Assert.That(AttackingStageExecutionManager.TryActivateRoot(em, battle, root), Is.True);
            SetPhaseForTest(BattlePhase.Attacking);

            Entity extraTurn = CreateExtraTurn(root);
            Assert.That(
                AttackingStageExecutionManager.TryBeginChild(em, battle, extraTurn),
                Is.True
            );
            SetPhaseForTest(BattlePhase.Attacking);

            Entity extraAction = CreateExtraAction(extraTurn);
            Assert.That(
                AttackingStageExecutionManager.TryBeginChild(em, battle, extraAction),
                Is.True
            );
            Assert.That(
                em.GetComponentData<BattleState>(battle).Phase,
                Is.EqualTo(BattlePhase.Planning)
            );
            SetPhaseForTest(BattlePhase.Attacking);
            Assert.That(
                AttackingStageExecutionManager.TryCompleteChildAndResume(em, battle),
                Is.True
            );
            Assert.That(Active, Is.EqualTo(extraTurn));
            Assert.That(
                Context(root).ExecutionStatus,
                Is.EqualTo(AttackingStageExecutionStatus.Suspended)
            );
            Assert.That(
                AttackingStageExecutionManager.TryCompleteChildAndResume(em, battle),
                Is.True
            );
            Assert.That(Active, Is.EqualTo(root));
        }

        [Test]
        public void CannotStartChildBeforeAttackingOrWhenParentNotActive()
        {
            Entity root = CreateRoot(playerA);
            Assert.That(AttackingStageExecutionManager.TryActivateRoot(em, battle, root), Is.True);
            Entity child = CreateExtraTurn(root);
            Assert.That(AttackingStageExecutionManager.TryBeginChild(em, battle, child), Is.False);
            Assert.That(Active, Is.EqualTo(root));
            Assert.That(
                Context(root).ExecutionStatus,
                Is.EqualTo(AttackingStageExecutionStatus.Active)
            );

            SetPhaseForTest(BattlePhase.Attacking);
            Assert.That(AttackingStageExecutionManager.TryBeginChild(em, battle, child), Is.True);
            Assert.That(AttackingStageExecutionManager.TryBeginChild(em, battle, child), Is.False);
        }

        [Test]
        public void CannotCreateChildFromAnotherBattle()
        {
            Entity root = CreateRoot(playerA);
            Entity otherBattle = em.CreateEntity();
            em.AddComponent<BattleTag>(otherBattle);
            em.AddComponentData(
                otherBattle,
                new AttackingStageExecutionInstance { Context = Entity.Null }
            );
            em.AddComponentData(
                otherBattle,
                new AttackingStageExecutionInstanceCounter { NextID = 1 }
            );

            uint before = em.GetComponentData<AttackingStageExecutionInstanceCounter>(
                otherBattle
            ).NextID;
            bool created = AttackingStageExecutionContextFactory.TryCreate(
                em,
                otherBattle,
                playerB,
                AttackingStageExecutionType.ExtraTurn,
                root,
                Entity.Null,
                out Entity rejected
            );

            Assert.That(created, Is.False);
            Assert.That(rejected, Is.EqualTo(Entity.Null));
            Assert.That(
                em.GetComponentData<AttackingStageExecutionInstanceCounter>(otherBattle).NextID,
                Is.EqualTo(before)
            );
        }

        [Test]
        public void RootCanBeRetiredAndNextNormalRootActivated()
        {
            Entity root = CreateRoot(playerA);
            Assert.That(AttackingStageExecutionManager.TryActivateRoot(em, battle, root), Is.True);
            SetPhaseForTest(BattlePhase.TurnEnd);
            Assert.That(AttackingStageExecutionManager.TryCompleteRoot(em, battle), Is.True);
            Assert.That(Active, Is.EqualTo(Entity.Null));
            Assert.That(
                Context(root).ExecutionStatus,
                Is.EqualTo(AttackingStageExecutionStatus.Completed)
            );

            Entity next = CreateRoot(playerB);
            Assert.That(AttackingStageExecutionManager.TryActivateRoot(em, battle, next), Is.True);
            Assert.That(Active, Is.EqualTo(next));
            Assert.That(
                em.GetComponentData<BattleState>(battle).Phase,
                Is.EqualTo(BattlePhase.TurnStart)
            );
        }
    }
}
