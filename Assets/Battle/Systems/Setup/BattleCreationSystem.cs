using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Content.Registries;
using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Setup
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleCreationGroup))]
    public partial struct BattleCreationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StartBattleRequest>();
            state.RequireForUpdate<BattleContentReadyTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (request, loadout, requestEntity) in SystemAPI
                    .Query<RefRO<StartBattleRequest>, DynamicBuffer<BattleLoadoutEntry>>()
                    .WithEntityAccess()
            )
            {
                // BATTLE RELATED CREATION
                Entity battleEntity = ecb.CreateEntity();
                InitialiseBattle(ecb, battleEntity, request.ValueRO);

                // PLAYER RELATED CREATION
                Entity playerA = ecb.CreateEntity();
                InitialisePlayer(ecb, playerA, battleEntity, BattleSide.SideA, loadout);

                Entity playerB = ecb.CreateEntity();
                InitialisePlayer(ecb, playerB, battleEntity, BattleSide.SideB, loadout);

                //DESTROY BATTLE START REQUEST ENTITY
                ecb.DestroyEntity(requestEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void InitialiseBattle(
            EntityCommandBuffer ecb,
            Entity battle,
            StartBattleRequest request
        )
        {
            DeterministicRNG rng = new DeterministicRNG(request.BattleSeed);
            ulong BattleID = request.BattleID;
            // BATTLE RELATED COMPONENTS / BUFFERS
            ecb.AddComponent<BattleTag>(battle);
            ecb.AddComponent(battle, new BattleID { Value = BattleID });
            ecb.AddComponent(battle, new BattleRNG { StateA = rng.StateA, StateB = rng.StateB });
            ecb.AddComponent(battle, new BattleState { Phase = BattlePhase.Creating });
            ecb.AddComponent(battle, new BattleRuntimeIDCounter { NextID = 100 });
            ecb.AddComponent(battle, new BattleEventFrameIDCounter { NextID = 1 });
            ecb.AddComponent(battle, new BattleEventGroupIDCounter { NextID = 1 });
            ecb.AddComponent(battle, new BattleActionExecutionCounter { NextID = 1 });
            ecb.AddComponent(battle, new BattleOperationIDCounter { NextID = 1 });
            ecb.AddComponent(battle, new PresentationStateRevision { Value = 0 });
            ecb.AddComponent(battle, new ActiveTurnPlayer { });
            ecb.AddComponent(
                battle,
                new BattleContentRegistry
                {
                    BattleRegistryReference = SystemAPI
                        .GetSingleton<ContentBlobRegistryComponent>()
                        .BlobRegistryReference,
                }
            );
            ecb.AddBuffer<BattleParticipant>(battle);

            // EVENT RELATED COMPONENTS / BUFFERS
            ecb.AddBuffer<BattleEvent>(battle);
            ecb.AddBuffer<ChainedBattleEvent>(battle);
            ecb.AddBuffer<BehaviourExecutionRequest>(battle);
            ecb.AddBuffer<ActionExecutionRequest>(battle);
            ecb.AddBuffer<ActionExecutionState>(battle);

            // PRESENTATION RELATED COMPONENTS / BUFFERS
            ecb.AddComponent(battle, new PresentationSequenceCounter { NextSequence = 1 });
            ecb.AddBuffer<PresentationFact>(battle);
        }

        private void InitialisePlayer(
            EntityCommandBuffer ecb,
            Entity player,
            Entity battle,
            BattleSide side,
            DynamicBuffer<BattleLoadoutEntry> allEntries
        )
        {
            ecb.AddComponent<PlayerTag>(player);

            ecb.AddComponent(player, new OwnedBattle { Battle = battle });

            ecb.AddComponent(player, new Team { Side = side });

            ecb.AddComponent(player, new SelectedCharacter { Value = Entity.Null });

            ecb.AddBuffer<DeckCard>(player);
            ecb.AddBuffer<HandCard>(player);
            ecb.AddBuffer<FieldCard>(player);

            DynamicBuffer<BattleDeckEntry> deck = ecb.AddBuffer<BattleDeckEntry>(player);
            foreach (BattleLoadoutEntry entry in allEntries)
            {
                if (entry.Side == side)
                {
                    deck.Add(
                        new BattleDeckEntry
                        {
                            CardDefinitionID = entry.CardDefinitionID,
                            CardType = entry.CardType,
                        }
                    );
                }
            }
        }
    }
}
