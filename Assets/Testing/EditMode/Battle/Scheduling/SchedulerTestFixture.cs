using System;
using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Data.VM;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Runtime;
using Archeus.Battle.Scheduling;
using Archeus.Battle.Snapshots;
using Archeus.Battle.Systems.Actions;
using Archeus.Battle.Systems.Events;
using Archeus.Battle.Systems.Setup;
using Archeus.Battle.Systems.Turnflow;
using Archeus.Content.Registries;
using Archeus.Testing.Battle.Snapshots;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Testing.Battle.Scheduling
{
    internal sealed class SchedulerTestFixture : IDisposable
    {
        public readonly SnapshotTestFixture Snapshot;
        public World World => Snapshot.World;
        public EntityManager EM => World.EntityManager;
        public Entity Battle => Snapshot.Battle;
        public Entity Player => Snapshot.Player;
        public Entity Opponent => Snapshot.Opponent;
        public Entity Context => Snapshot.Context;
        public readonly Entity Mei;
        public readonly Entity Toma;
        public readonly Entity Harun;
        public readonly Entity Karathos;
        public readonly Entity Enemy;
        public readonly SystemHandle Dispatch;
        public readonly SystemHandle Events;
        public readonly SystemHandle Phases;
        public readonly SystemHandle Attacking;
        public readonly SystemHandle Planning;
        public readonly SystemHandle Drawing;
        private BlobAssetReference<ContentBlobRegistry> content;

        public SchedulerTestFixture(
            bool interrupt = true,
            ActionControlKind reaction = ActionControlKind.ReplaceCurrent,
            World sharedWorld = null,
            ulong battleID = 42,
            BattleEventType reactionEvent = BattleEventType.ActionAttemptStarted
        )
        {
            Snapshot = new SnapshotTestFixture(sharedWorld, battleID);
            var em = EM;
            Mei = Snapshot.Card;
            em.GetBuffer<DeckCard>(Player).Clear();
            ConfigureActor(Mei, 10, Player);
            Toma = Actor(101, 20, Player);
            Harun = Actor(102, 30, Player);
            Karathos = Actor(103, 40, Player);
            Enemy = Actor(200, 15, Opponent);
            em.AddComponentData(
                Karathos,
                new ActionEligibility
                {
                    ManualMask = 0,
                    AutomaticMask = ActionEligibility.Mask(CharacterActionType.SuperchargedAttack),
                }
            );
            em.AddBuffer<BattlePlayerReference>(Battle)
                .Add(new BattlePlayerReference { Player = Player });
            em.GetBuffer<BattlePlayerReference>(Battle)
                .Add(new BattlePlayerReference { Player = Opponent });
            em.AddBuffer<BattleParticipant>(Battle);
            foreach (Entity actor in new[] { Mei, Toma, Harun, Karathos, Enemy })
                em.GetBuffer<BattleParticipant>(Battle)
                    .Add(new BattleParticipant { Participant = actor });
            content = BuildContent(reaction, reactionEvent);
            em.AddComponentData(
                Battle,
                new BattleContentRegistry { BattleRegistryReference = content }
            );
            if (interrupt)
            {
                em.AddBuffer<BehaviourReference>(Karathos)
                    .Add(new BehaviourReference { BehaviourIndex = 0 });
                em.GetBuffer<BehaviourRuntimeState>(Karathos).Add(default);
            }
            em.AddBuffer<BehaviourReference>(Toma)
                .Add(new BehaviourReference { BehaviourIndex = 1 });
            em.GetBuffer<BehaviourRuntimeState>(Toma).Add(default);
            em.AddBuffer<ActionProgramBinding>(Karathos)
                .Add(
                    new ActionProgramBinding
                    {
                        Type = CharacterActionType.SuperchargedAttack,
                        DefinitionID = 0,
                        EnergyCost = 5,
                        TargetMode = ActionTargetMode.SingleEnemy,
                        ProgramIndex = 1,
                    }
                );
            Dispatch = World.CreateSystem<ActionExecutionSystem>();
            Events = World.CreateSystem<BattleEventProcessingSystem>();
            Phases = World.CreateSystem<BattlePhaseTransitionSystem>();
            Attacking = World.CreateSystem<AttackingStageSystem>();
            Planning = World.CreateSystem<PlanningStageSystem>();
            Drawing = World.CreateSystem<DrawingStageSystem>();
            Phase(BattlePhase.Planning);
            Snapshot.Open();
        }

        private void ConfigureActor(Entity actor, float attack, Entity player)
        {
            EM.SetComponentData(actor, new CardOwner { Player = player });
            EM.AddComponent<Archeus.Battle.Components.Tags.CharacterTag>(actor);
            EM.AddComponentData(actor, new CardDefinitionID { Value = 1 });
            EM.AddComponentData(actor, EM.GetComponentData<BattleSideAssignment>(player));
            EM.SetComponentData(actor, new CurrentHealth { Value = 1000 });
            EM.SetComponentData(actor, new HeldSuperchargedEnergy { Value = 5 });
            EM.AddComponentData(
                actor,
                new CharacterRuntimeStatBaseline
                {
                    Attack = attack,
                    MaxHealth = 1000,
                    Defense = 0,
                    CritRATE = 0,
                    CritDMG = 0,
                }
            );
            EM.GetBuffer<FieldCard>(player).Add(new FieldCard { Card = actor });
        }

        public Entity Actor(uint id, float attack, Entity player)
        {
            Entity actor = Snapshot.CreateCard(id);
            ConfigureActor(actor, attack, player);
            return actor;
        }

        public void Phase(BattlePhase phase) =>
            Assert.That(AttackingStageExecutionManager.SetPhase(EM, Battle, phase), Is.True);

        public uint ContextID =>
            EM.GetComponentData<AttackingStageExecutionContext>(Context).ContextID;

        public uint Plan(
            Entity actor,
            Entity target,
            CharacterActionType type = CharacterActionType.NormalAttack,
            uint definitionID = 0
        )
        {
            var action = new PlannedAction
            {
                ActionType = PlannedActionType.CharacterAction,
                Source = actor,
                PrimaryTarget = target,
                CharacterAction = type,
                DefinitionID = definitionID,
            };
            Assert.That(
                ActionQueueService.TryPlan(
                    EM,
                    Battle,
                    EM.GetComponentData<AttackingStageExecutionContext>(Context).ActingPlayer,
                    action,
                    out uint id
                ),
                Is.True
            );
            return id;
        }

        public void Commit()
        {
            Entity request = EM.CreateEntity();
            EM.AddComponentData(
                request,
                new EndPlanningRequest
                {
                    Player = EM.GetComponentData<AttackingStageExecutionContext>(
                        Context
                    ).ActingPlayer,
                }
            );
            Planning.Update(World.Unmanaged);
            Phases.Update(World.Unmanaged);
            Snapshot.Snapshots.Update();
            Assert.That(
                EM.GetComponentData<BattleState>(Battle).Phase,
                Is.EqualTo(BattlePhase.Attacking)
            );
        }

        public void RunAction()
        {
            Dispatch.Update(World.Unmanaged);
            Events.Update(World.Unmanaged);
        }

        public void FinishStage()
        {
            Attacking.Update(World.Unmanaged);
            Phases.Update(World.Unmanaged);
            Snapshot.Snapshots.Update();
        }

        public ActionControlRequest Control(
            ActionControlKind kind,
            Entity actor = default,
            Entity target = default
        ) =>
            new ActionControlRequest
            {
                ContextID = ContextID,
                Kind = kind,
                Action = new PlannedAction
                {
                    ActionType = PlannedActionType.CharacterAction,
                    Source = actor == Entity.Null ? Mei : actor,
                    PrimaryTarget = target == Entity.Null ? Enemy : target,
                    CharacterAction = CharacterActionType.NormalAttack,
                },
            };

        public ulong Apply(ActionControlRequest control)
        {
            Assert.That(ActionControlProcessor.Enqueue(EM, Battle, control, out ulong id), Is.True);
            ActionControlProcessor.ApplyFrame(EM, Battle, control.FrameID);
            return id;
        }

        public ActionOrderSnapshot Projection => ActionOrderPublication.Capture(EM, Battle);

        public float HP(Entity actor) => EM.GetComponentData<CurrentHealth>(actor).Value;

        public void Dispose()
        {
            EM.RemoveComponent<BattleContentRegistry>(Battle);
            Snapshot.Dispose();
            content.Dispose();
        }

        private static BlobAssetReference<ContentBlobRegistry> BuildContent(
            ActionControlKind reaction,
            BattleEventType reactionEvent
        )
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref ContentBlobRegistry root = ref builder.ConstructRoot<ContentBlobRegistry>();
            var behaviours = builder.Allocate(ref root.Behaviours, 2);
            for (int i = 0; i < 2; i++)
            {
                var triggers = builder.Allocate(ref behaviours[i].Triggers, 1);
                triggers[0].EventType = i == 0 ? reactionEvent : BattleEventType.ActionPerformed;
                triggers[0].Phase = BattleEventPhase.PreResolution;
                triggers[0].OwnerType = TriggerOwnerType.Any;
                triggers[0].VMProgramIndex = i == 0 ? 0 : 2;
                triggers[0].Priority = 10;
                var conditions = builder.Allocate(ref triggers[0].Conditions, 2);
                conditions[0] = new Archeus.Content.Blobs.EventConditionBlob
                {
                    Type = ConditionType.RuntimeIDEquals,
                    Target = ConditionTarget.Source,
                    ReferenceID = 101,
                };
                conditions[1] = new Archeus.Content.Blobs.EventConditionBlob
                {
                    Type = ConditionType.ActionTypeEquals,
                    Target = ConditionTarget.Source,
                    ReferenceID = (uint)CharacterActionType.SuperchargedAttack,
                };
            }
            var effects = builder.Allocate(ref root.Effects, 1);
            builder.Allocate(ref effects[0].StatModifiers, 0);
            builder.Allocate(ref effects[0].BehaviourIndices, 0);
            var programs = builder.Allocate(ref root.AbilityPrograms, 5);
            var interrupt = builder.Allocate(ref programs[0].Instructions, 2);
            interrupt[0] = new AbilityInstruction
            {
                Opcode = AbilityOpcode.RequestActionControl,
                A = (int)reaction,
                B = (int)CharacterActionType.SuperchargedAttack,
            };
            interrupt[1] = new AbilityInstruction { Opcode = AbilityOpcode.End };
            var attack = builder.Allocate(ref programs[1].Instructions, 5);
            attack[0] = new AbilityInstruction { Opcode = AbilityOpcode.PushInheritedStat, A = 0 };
            attack[1] = new AbilityInstruction
            {
                Opcode = AbilityOpcode.PushConst,
                A = VMEncoding.EncodeFloat(10),
            };
            attack[2] = new AbilityInstruction { Opcode = AbilityOpcode.Div };
            attack[3] = new AbilityInstruction
            {
                Opcode = AbilityOpcode.DealDamage,
                Flags =
                    AbilityInstructionFlags.GameplayOperation
                    | AbilityInstructionFlags.StartsActionHit,
            };
            attack[4] = new AbilityInstruction { Opcode = AbilityOpcode.End };
            var performed = builder.Allocate(ref programs[2].Instructions, 5);
            performed[0] = new AbilityInstruction { Opcode = AbilityOpcode.LoadState, A = 0 };
            performed[1] = new AbilityInstruction
            {
                Opcode = AbilityOpcode.PushConst,
                A = VMEncoding.EncodeFloat(1),
            };
            performed[2] = new AbilityInstruction { Opcode = AbilityOpcode.Add };
            performed[3] = new AbilityInstruction { Opcode = AbilityOpcode.StoreState, A = 0 };
            performed[4] = new AbilityInstruction { Opcode = AbilityOpcode.End };
            var multi = builder.Allocate(ref programs[3].Instructions, 5);
            multi[0] = new AbilityInstruction
            {
                Opcode = AbilityOpcode.PushConst,
                A = VMEncoding.EncodeFloat(1),
            };
            multi[1] = new AbilityInstruction
            {
                Opcode = AbilityOpcode.DealDamage,
                Flags =
                    AbilityInstructionFlags.GameplayOperation
                    | AbilityInstructionFlags.StartsActionHit,
            };
            multi[2] = multi[0];
            multi[3] = multi[1];
            multi[4] = new AbilityInstruction { Opcode = AbilityOpcode.End };
            var loop = builder.Allocate(ref programs[4].Instructions, 1);
            loop[0] = new AbilityInstruction { Opcode = AbilityOpcode.Jump, A = 0 };
            return builder.CreateBlobAssetReference<ContentBlobRegistry>(Allocator.Persistent);
        }
    }
}
