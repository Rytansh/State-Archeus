using System;
using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Effects;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Runtime;
using Archeus.Battle.Scheduling;
using Archeus.Battle.Snapshots;
using Archeus.Game.Bootstrap;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Testing.Battle.Snapshots
{
    internal sealed class SnapshotTestFixture : IDisposable
    {
        public readonly World World;
        private readonly bool ownsWorld;
        public EntityManager Entities => World.EntityManager;
        public readonly Entity Battle;
        public readonly Entity Player;
        public readonly Entity Opponent;
        public readonly Entity Card;
        public Entity Context =>
            Entities.GetComponentData<AttackingStageExecutionInstance>(Battle).Context;
        public readonly BattleStageSnapshotSystem Snapshots;

        public SnapshotTestFixture(World sharedWorld = null, ulong battleID = 42)
        {
            ownsWorld = sharedWorld == null;
            World = sharedWorld ?? new World("Stage snapshot Edit Mode tests");
            var entities = Entities;
            Battle = entities.CreateEntity();
            entities.AddComponent<BattleTag>(Battle);
            entities.AddComponentData(Battle, new BattleID { Value = battleID });
            entities.AddComponentData(Battle, new BattleState { Phase = BattlePhase.Drawing });
            entities.AddComponentData(Battle, new TurnCounter { CurrentTurn = 1 });
            var rng = new DeterministicRNG(12345678);
            entities.AddComponentData(
                Battle,
                new BattleRNG { StateA = rng.StateA, StateB = rng.StateB }
            );
            entities.AddComponentData(Battle, new BattleRuntimeIDCounter { NextID = 101 });
            entities.AddComponentData(Battle, new BattleActionExecutionCounter { NextID = 1 });
            entities.AddComponentData(Battle, new BattleOperationIDCounter { NextID = 1 });
            entities.AddComponentData(Battle, new BattleEventFrameIDCounter { NextID = 1 });
            entities.AddComponentData(Battle, new BattleEventGroupIDCounter { NextID = 1 });
            entities.AddComponentData(Battle, new PresentationSequenceCounter { NextSequence = 1 });
            entities.AddBuffer<BattleEvent>(Battle);
            entities.AddBuffer<ChainedBattleEvent>(Battle);
            entities.AddBuffer<BehaviourExecutionRequest>(Battle);
            entities.AddBuffer<ActionExecutionRequest>(Battle);
            entities.AddBuffer<ActionExecutionState>(Battle);
            entities.AddBuffer<PresentationFact>(Battle);
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ActionQueueService.AddBattleData(ecb, Battle);
                ecb.Playback(entities);
            }
            entities.AddComponentData(Battle, new AttackingStageExecutionInstance());
            entities.AddComponentData(
                Battle,
                new AttackingStageExecutionInstanceCounter { NextID = 1 }
            );
            entities.AddComponentData(Battle, new BattlePhaseEpoch());
            Player = CreatePlayer(1001, BattleSide.SideA);
            Opponent = CreatePlayer(1002, BattleSide.SideB);
            entities.AddComponentData(Battle, new ActiveTurnPlayer { Player = Player });
            entities.SetComponentData(Battle, new BattleState { Phase = BattlePhase.Setup });
            if (
                !AttackingStageExecutionContextFactory.TryCreate(
                    entities,
                    Battle,
                    Player,
                    AttackingStageExecutionType.NormalTurn,
                    Entity.Null,
                    Entity.Null,
                    out Entity context
                ) || !AttackingStageExecutionManager.TryActivateRoot(entities, Battle, context)
            )
                throw new InvalidOperationException("Snapshot test context setup failed.");
            entities.SetComponentData(Battle, new BattleState { Phase = BattlePhase.Drawing });
            Card = CreateCard(100);
            entities.GetBuffer<DeckCard>(Player).Add(new DeckCard { Card = Card });
            Snapshots = World.GetOrCreateSystemManaged<BattleStageSnapshotSystem>();
        }

        private Entity CreatePlayer(ulong id, BattleSide side)
        {
            var entities = Entities;
            Entity player = entities.CreateEntity();
            entities.AddComponent<PlayerTag>(player);
            entities.AddComponentData(player, new BattlePlayerIdentity { PlayerID = id });
            entities.AddComponentData(player, new BattleSideAssignment { Side = side });
            entities.AddComponentData(player, new OwnedBattle { Battle = Battle });
            entities.AddComponentData(player, new RemainingActionPoints { Value = 4 });
            entities.AddComponentData(player, new MaxActionPoints { Value = 4 });
            entities.AddComponentData(player, new MaxHandSize { Value = 4 });
            entities.AddBuffer<DeckCard>(player);
            entities.AddBuffer<HandCard>(player);
            entities.AddBuffer<FieldCard>(player);
            return player;
        }

        public Entity CreateCard(uint id)
        {
            var entities = Entities;
            Entity card = entities.CreateEntity();
            entities.AddComponentData(card, new OwnedBattle { Battle = Battle });
            entities.AddComponentData(card, new CardOwner { Player = Player });
            entities.AddComponentData(card, new CardRuntimeID { Value = id });
            entities.AddComponentData(card, new CurrentHealth { Value = 100 });
            entities.AddComponentData(card, new HeldSuperchargedEnergy { Value = 3 });
            entities.AddComponent<Alive>(card);
            entities.AddBuffer<ActiveEffect>(card);
            entities.AddBuffer<BasicEffectInstance>(card);
            entities.AddBuffer<DOTEffectInstance>(card);
            entities.AddBuffer<BehaviourRuntimeState>(card);
            return card;
        }

        public ulong Open()
        {
            Snapshots.Update();
            return Entities.GetComponentData<StageSnapshotStatus>(Battle).Revision;
        }

        public void Dispose()
        {
            if (ownsWorld)
                World.Dispose();
        }
    }
}
