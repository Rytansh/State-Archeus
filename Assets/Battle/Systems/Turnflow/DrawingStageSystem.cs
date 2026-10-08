using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Runtime;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Turnflow
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(DrawingStageGroup))]
    public partial struct DrawingStageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            ProcessDrawCardRequests(ref state, ecb);
            ProcessPlaceCardRequests(ref state, ecb);
            ProcessRetreatCardRequests(ref state, ecb);
            ProcessEndDrawingRequests(ref state, ecb);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void ProcessDrawCardRequests(ref SystemState state, EntityCommandBuffer ecb)
        {
            foreach (
                var (request, requestEntity) in SystemAPI
                    .Query<RefRO<DrawCardsRequest>>()
                    .WithEntityAccess()
            )
            {
                Entity player = request.ValueRO.Player;

                if (
                    !TurnCommandValidator.TryGetActiveBattle(
                        ref state,
                        player,
                        BattlePhase.Drawing,
                        out Entity battle
                    )
                    || SystemAPI.HasComponent<BattleDrawingCompleteTag>(battle)
                    || !SystemAPI.HasComponent<MaxHandSize>(player)
                    || !SystemAPI.HasBuffer<DeckCard>(player)
                    || !SystemAPI.HasBuffer<HandCard>(player)
                    || !SystemAPI.HasComponent<BattleRNG>(battle)
                )
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                RefRW<BattleRNG> battleRNG = SystemAPI.GetComponentRW<BattleRNG>(battle);
                MaxHandSize maxHandSize = SystemAPI.GetComponent<MaxHandSize>(player);
                DynamicBuffer<DeckCard> deck = SystemAPI.GetBuffer<DeckCard>(player);
                DynamicBuffer<HandCard> hand = SystemAPI.GetBuffer<HandCard>(player);

                while (hand.Length < maxHandSize.Value && deck.Length > 0)
                {
                    bool drawn = CardZoneResolver.TryDrawRandomCard(
                        ref battleRNG.ValueRW,
                        ref deck,
                        ref hand,
                        maxHandSize.Value,
                        out Entity drawnCard
                    );

                    if (!drawn)
                        break;

                    if (SystemAPI.HasComponent<CardRuntimeID>(drawnCard))
                    {
                        uint runtimeID = SystemAPI.GetComponent<CardRuntimeID>(drawnCard).Value;

                        Logging.Info(
                            LogCategory.Combat,
                            $"Card {runtimeID} drawn by player {player.Index}."
                        );
                    }
                }

                ecb.DestroyEntity(requestEntity);
            }
        }

        private void ProcessPlaceCardRequests(ref SystemState state, EntityCommandBuffer ecb)
        {
            foreach (
                var (request, requestEntity) in SystemAPI
                    .Query<RefRO<PlaceCardRequest>>()
                    .WithEntityAccess()
            )
            {
                Entity player = request.ValueRO.Player;
                Entity card = request.ValueRO.CardToPlace;

                if (
                    !TurnCommandValidator.TryGetActiveBattle(
                        ref state,
                        player,
                        BattlePhase.Drawing,
                        out Entity battle
                    )
                    || SystemAPI.HasComponent<BattleDrawingCompleteTag>(battle)
                    || !SystemAPI.HasComponent<RemainingActionPoints>(player)
                    || !SystemAPI.HasBuffer<HandCard>(player)
                    || !SystemAPI.HasBuffer<FieldCard>(player)
                )
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                if (card == Entity.Null || !state.EntityManager.Exists(card))
                {
                    Logging.Warn(LogCategory.Combat, "Attempted to place an invalid card.");

                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                RefRW<RemainingActionPoints> remainingAP =
                    SystemAPI.GetComponentRW<RemainingActionPoints>(player);

                if (remainingAP.ValueRO.Value <= 0)
                {
                    Logging.Warn(LogCategory.Combat, "Not enough AP to place card.");

                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                if (SystemAPI.HasComponent<RetreatedThisTurn>(card))
                {
                    Logging.Warn(
                        LogCategory.Combat,
                        "This card was retreated during the current turn."
                    );

                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                DynamicBuffer<HandCard> hand = SystemAPI.GetBuffer<HandCard>(player);

                DynamicBuffer<FieldCard> field = SystemAPI.GetBuffer<FieldCard>(player);

                bool deployed = CardZoneResolver.TryDeployCard(
                    card,
                    request.ValueRO.Position,
                    ref hand,
                    ref field
                );

                if (deployed)
                {
                    remainingAP.ValueRW.Value--;

                    Logging.Info(
                        LogCategory.Combat,
                        $"Card {card.Index} deployed to {request.ValueRO.Position}."
                    );

                    // Later:
                    // - emit CardPlaced / CharacterDeployed
                    // - resolve placement-triggered behaviours
                }
                else
                {
                    Logging.Warn(LogCategory.Combat, "Card could not be deployed.");
                }

                ecb.DestroyEntity(requestEntity);
            }
        }

        private void ProcessRetreatCardRequests(ref SystemState state, EntityCommandBuffer ecb)
        {
            foreach (
                var (request, requestEntity) in SystemAPI
                    .Query<RefRO<RetreatCardRequest>>()
                    .WithEntityAccess()
            )
            {
                Entity player = request.ValueRO.Player;
                Entity card = request.ValueRO.CardToRetreat;

                if (
                    !TurnCommandValidator.TryGetActiveBattle(
                        ref state,
                        player,
                        BattlePhase.Drawing,
                        out Entity battle
                    )
                    || SystemAPI.HasComponent<BattleDrawingCompleteTag>(battle)
                    || !SystemAPI.HasComponent<RemainingActionPoints>(player)
                    || !SystemAPI.HasComponent<MaxHandSize>(player)
                    || !SystemAPI.HasBuffer<FieldCard>(player)
                    || !SystemAPI.HasBuffer<HandCard>(player)
                )
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                if (card == Entity.Null || !state.EntityManager.Exists(card))
                {
                    Logging.Warn(LogCategory.Combat, "Attempted to retreat an invalid card.");

                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                // Skills, Primordial Blizzard, etc. can use this generic
                // restriction rather than hard-coding card types here.
                if (SystemAPI.HasComponent<CannotRetreatTag>(card))
                {
                    Logging.Warn(LogCategory.Combat, "This card cannot retreat.");

                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                RefRW<RemainingActionPoints> remainingAP =
                    SystemAPI.GetComponentRW<RemainingActionPoints>(player);

                if (remainingAP.ValueRO.Value <= 0)
                {
                    Logging.Warn(LogCategory.Combat, "Not enough AP to retreat card.");

                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                MaxHandSize maxHandSize = SystemAPI.GetComponent<MaxHandSize>(player);

                DynamicBuffer<FieldCard> field = SystemAPI.GetBuffer<FieldCard>(player);

                DynamicBuffer<HandCard> hand = SystemAPI.GetBuffer<HandCard>(player);

                bool retreated = CardZoneResolver.TryRetreatCard(
                    card,
                    ref field,
                    ref hand,
                    maxHandSize.Value
                );

                if (retreated)
                {
                    remainingAP.ValueRW.Value--;

                    if (!SystemAPI.HasComponent<RetreatedThisTurn>(card))
                    {
                        ecb.AddComponent<RetreatedThisTurn>(card);
                    }

                    Logging.Info(LogCategory.Combat, $"Card {card.Index} retreated.");

                    // Later:
                    // - clear effects where appropriate
                    // - clear supercharged energy
                    // - destroy attached skills
                    // - update retreat counters
                    // - emit CharacterRetreated
                    // - resolve resulting behaviours
                }
                else
                {
                    Logging.Warn(LogCategory.Combat, "Card could not be retreated.");
                }

                ecb.DestroyEntity(requestEntity);
            }
        }

        private void ProcessEndDrawingRequests(ref SystemState state, EntityCommandBuffer ecb)
        {
            foreach (
                var (request, requestEntity) in SystemAPI
                    .Query<RefRO<EndDrawingRequest>>()
                    .WithEntityAccess()
            )
            {
                Entity player = request.ValueRO.Player;

                if (
                    !TurnCommandValidator.TryGetActiveBattle(
                        ref state,
                        player,
                        BattlePhase.Drawing,
                        out Entity battle
                    ) || SystemAPI.HasComponent<BattleDrawingCompleteTag>(battle)
                )
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                ecb.AddComponent<BattleDrawingCompleteTag>(battle);

                Logging.Info(
                    LogCategory.Combat,
                    $"Player {player.Index} completed the Drawing Stage."
                );

                ecb.DestroyEntity(requestEntity);
            }
        }
    }
}
