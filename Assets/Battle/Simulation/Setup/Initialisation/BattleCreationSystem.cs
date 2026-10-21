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
                var (request, players, cards, requestEntity) in SystemAPI
                    .Query<
                        RefRO<StartBattleRequest>,
                        DynamicBuffer<BattlePlayerEntry>,
                        DynamicBuffer<PlayerDeckEntry>
                    >()
                    .WithEntityAccess()
            )
            {
                if (!CheckIfPlayerDataValid(players))
                {
                    //throw an error etc
                    continue;
                }

                if (!CheckIfCardOwnersValid(players, cards))
                {
                    //throw an error etc
                    continue;
                }

                BattleRNG battleRNG = InitialiseRNG(request.ValueRO);

                bool lowerIDGetsSideA = BattleRNGService.RollBool(ref battleRNG);

                ulong lowerID =
                    players[0].PlayerID < players[1].PlayerID
                        ? players[0].PlayerID
                        : players[1].PlayerID;

                Entity battleEntity = ecb.CreateEntity();

                AddComponentsToBattle(ecb, battleEntity, request.ValueRO, battleRNG);

                DynamicBuffer<BattlePlayerReference> battlePlayers =
                    ecb.AddBuffer<BattlePlayerReference>(battleEntity);

                foreach (BattlePlayerEntry entry in players)
                {
                    bool isLowerID = entry.PlayerID == lowerID;

                    BattleSide side =
                        isLowerID == lowerIDGetsSideA ? BattleSide.SideA : BattleSide.SideB;

                    Entity player = ecb.CreateEntity();

                    AddComponentsToPlayer(ecb, player, battleEntity, entry.PlayerID, side);

                    battlePlayers.Add(new BattlePlayerReference { Player = player });
                }

                ecb.RemoveComponent<StartBattleRequest>(requestEntity);

                ecb.AddComponent(
                    requestEntity,
                    new InitialRosterConstructionRequest { Battle = battleEntity }
                );

                ecb.AddComponent<BattleInitialisationCompleteTag>(battleEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void AddComponentsToBattle(
            EntityCommandBuffer ecb,
            Entity battle,
            StartBattleRequest request,
            BattleRNG battleRNG
        )
        {
            // BATTLE RELATED COMPONENTS / BUFFERS
            ecb.AddComponent<BattleTag>(battle);
            ecb.AddComponent(battle, new BattleID { Value = request.BattleID });
            ecb.AddComponent(battle, battleRNG);
            ecb.AddComponent(battle, new BattleState { Phase = BattlePhase.Creating });
            ecb.AddComponent(battle, new BattleRuntimeIDCounter { NextID = 100 });
            ecb.AddComponent(battle, new BattleEventFrameIDCounter { NextID = 1 });
            ecb.AddComponent(battle, new BattleEventGroupIDCounter { NextID = 1 });
            ecb.AddComponent(battle, new BattleActionExecutionCounter { NextID = 1 });
            ecb.AddComponent(battle, new BattleOperationIDCounter { NextID = 1 });
            ecb.AddComponent(battle, new PresentationStateRevision { Value = 0 });
            ecb.AddComponent(battle, new ActiveTurnPlayer { });
            ecb.AddComponent(battle, new TurnCounter { CurrentTurn = 0 });
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

        private void AddComponentsToPlayer(
            EntityCommandBuffer ecb,
            Entity player,
            Entity battle,
            ulong playerID,
            BattleSide side
        )
        {
            ecb.AddComponent<PlayerTag>(player);

            ecb.AddComponent(player, new BattlePlayerIdentity { PlayerID = playerID });

            ecb.AddComponent(player, new OwnedBattle { Battle = battle });

            ecb.AddComponent(player, new Team { Side = side });

            ecb.AddComponent(player, new SelectedCharacter { Value = Entity.Null });

            ecb.AddComponent(player, new MaxActionPoints { Value = 4 });
            ecb.AddComponent(player, new RemainingActionPoints { Value = 4 });
            ecb.AddComponent(player, new MaxHandSize { Value = 4 });
            ecb.AddComponent(player, new SelectedTarget { Value = Entity.Null });

            ecb.AddBuffer<DeckCard>(player);
            ecb.AddBuffer<HandCard>(player);
            ecb.AddBuffer<FieldCard>(player);
        }

        private BattleRNG InitialiseRNG(StartBattleRequest request)
        {
            DeterministicRNG initialRNG = new DeterministicRNG(request.BattleSeed);

            BattleRNG battleRNG = new BattleRNG
            {
                StateA = initialRNG.StateA,
                StateB = initialRNG.StateB,
            };

            return battleRNG;
        }

        private static bool CheckIfPlayerDataValid(DynamicBuffer<BattlePlayerEntry> players)
        {
            if (players.Length == 0)
                return false;

            for (int i = 0; i < players.Length; i++)
            {
                for (int j = i + 1; j < players.Length; j++)
                {
                    if (players[i].PlayerID == players[j].PlayerID)
                        return false;
                }
            }

            return true;
        }

        private static bool CheckIfCardOwnersValid(
            DynamicBuffer<BattlePlayerEntry> players,
            DynamicBuffer<PlayerDeckEntry> cards
        )
        {
            foreach (PlayerDeckEntry card in cards)
            {
                bool ownerFound = false;

                foreach (BattlePlayerEntry player in players)
                {
                    if (card.PlayerID == player.PlayerID)
                    {
                        ownerFound = true;
                        break;
                    }
                }

                if (!ownerFound)
                    return false;
            }

            return true;
        }
    }
}
