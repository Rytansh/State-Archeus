using System.Collections.Generic;
using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Presentation.State;
using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleRootGroup), OrderLast = true)]
    [UpdateAfter(typeof(PresentationSnapshotExportSystem))]
    public partial class PresentationUpdateExportSystem : SystemBase
    {
        private sealed class BattleProjectionCache
        {
            public BattlePresentationState Battle;

            public readonly Dictionary<uint, CharacterPresentationState> Characters = new();
            public readonly Dictionary<uint, CardPresentationState> Cards = new();
            public readonly Dictionary<BattleSide, HandPresentationState> Hands = new();
            public readonly Dictionary<BattleSide, PlayerPresentationState> Players = new();
        }

        private readonly Dictionary<ulong, BattleProjectionCache> projectionCaches = new();
        private readonly Dictionary<uint, CardPresentationState> currentCards = new();
        private readonly Dictionary<uint, CharacterPresentationState> currentCharacters = new();
        private readonly Dictionary<BattleSide, PlayerPresentationState> currentPlayers = new();
        private readonly Dictionary<BattleSide, HandPresentationState> currentHands = new();

        private EntityQuery bridgeQuery;
        private EntityQuery playerQuery;

        protected override void OnCreate()
        {
            bridgeQuery = GetEntityQuery(
                ComponentType.ReadOnly<BattlePresentationBridgeReference>()
            );

            playerQuery = GetEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<OwnedBattle>(),
                ComponentType.ReadOnly<Team>(),
                ComponentType.ReadOnly<RemainingActionPoints>(),
                ComponentType.ReadOnly<MaxHandSize>(),
                ComponentType.ReadOnly<DeckCard>(),
                ComponentType.ReadOnly<HandCard>(),
                ComponentType.ReadOnly<FieldCard>()
            );

            RequireForUpdate(bridgeQuery);
        }

        protected override void OnUpdate()
        {
            Entity bridgeEntity = bridgeQuery.GetSingletonEntity();

            BattlePresentationBridgeReference bridgeReference =
                EntityManager.GetComponentObject<BattlePresentationBridgeReference>(bridgeEntity);

            BattlePresentationBridge bridge = bridgeReference.Bridge;

            foreach (
                var (battleID, battleState, turnCounter, revision, battle) in SystemAPI
                    .Query<
                        RefRO<BattleID>,
                        RefRO<BattleState>,
                        RefRO<TurnCounter>,
                        RefRW<PresentationStateRevision>
                    >()
                    .WithAll<BattleTag, InitialPresentationStatePublishedTag>()
                    .WithEntityAccess()
            )
            {
                ulong id = battleID.ValueRO.Value;

                BattlePresentationState currentBattleState = PresentationStateBuilder.BuildBattle(
                    id,
                    in battleState.ValueRO,
                    in turnCounter.ValueRO
                );

                /*
                 * Initial snapshot has already been published.
                 * First observation by this system establishes
                 * the comparison baseline only.
                 */
                if (!TryBuildCurrentProjection(battle))
                {
                    continue;
                }

                if (!projectionCaches.TryGetValue(id, out BattleProjectionCache cache))
                {
                    cache = new BattleProjectionCache { Battle = currentBattleState };

                    foreach (var pair in currentCharacters)
                        cache.Characters.Add(pair.Key, pair.Value);

                    foreach (var pair in currentCards)
                        cache.Cards.Add(pair.Key, pair.Value);

                    foreach (var pair in currentHands)
                        cache.Hands.Add(pair.Key, pair.Value);

                    foreach (var pair in currentPlayers)
                        cache.Players.Add(pair.Key, pair.Value);

                    projectionCaches.Add(id, cache);

                    continue;
                }

                ExportBattleChange(bridge, id, revision, currentBattleState, cache);

                ExportPlayerChanges(bridge, id, revision, cache);

                ExportHandChanges(bridge, id, revision, cache);

                ExportCardChanges(bridge, id, revision, cache);

                ExportCharacterChanges(bridge, id, revision, cache);
            }
        }

        private void ExportBattleChange(
            BattlePresentationBridge bridge,
            ulong battleID,
            RefRW<PresentationStateRevision> revision,
            BattlePresentationState current,
            BattleProjectionCache cache
        )
        {
            if (EqualityComparer<BattlePresentationState>.Default.Equals(cache.Battle, current))
            {
                return;
            }

            ulong nextRevision = AllocateNextRevision(revision);

            bridge.PublishBattleStateUpdate(battleID, nextRevision, in current);

            cache.Battle = current;

            Logging.Info(
                LogCategory.Presentation,
                $"[STATE UPDATE EXPORT] "
                    + $"Battle={battleID} | "
                    + $"Revision={nextRevision} | "
                    + $"Phase={current.Phase} | "
                    + $"Turn={current.TurnNumber} | "
            );
        }

        private void ExportCharacterChanges(
            BattlePresentationBridge bridge,
            ulong battleID,
            RefRW<PresentationStateRevision> revision,
            BattleProjectionCache cache
        )
        {
            foreach (KeyValuePair<uint, CharacterPresentationState> pair in currentCharacters)
            {
                CharacterPresentationState current = pair.Value;

                if (
                    cache.Characters.TryGetValue(pair.Key, out CharacterPresentationState previous)
                    && EqualityComparer<CharacterPresentationState>.Default.Equals(
                        previous,
                        current
                    )
                )
                {
                    continue;
                }

                ulong nextRevision = AllocateNextRevision(revision);

                bridge.PublishCharacterStateUpdate(battleID, nextRevision, in current);

                cache.Characters[pair.Key] = current;

                Logging.Info(
                    LogCategory.Presentation,
                    $"[CHARACTER STATE UPDATE EXPORT] "
                        + $"Battle={battleID} | "
                        + $"Revision={nextRevision} | "
                        + $"Character={current.RuntimeID} | "
                        + $"HP={current.CurrentHealth}/"
                        + $"{current.MaxHealth} | "
                        + $"Alive={current.IsAlive}"
                );
            }
        }

        private void ExportCardChanges(
            BattlePresentationBridge bridge,
            ulong battleID,
            RefRW<PresentationStateRevision> revision,
            BattleProjectionCache cache
        )
        {
            foreach (KeyValuePair<uint, CardPresentationState> pair in currentCards)
            {
                CardPresentationState current = pair.Value;

                if (
                    cache.Cards.TryGetValue(pair.Key, out CardPresentationState previous)
                    && EqualityComparer<CardPresentationState>.Default.Equals(previous, current)
                )
                {
                    continue;
                }

                ulong nextRevision = AllocateNextRevision(revision);

                bridge.PublishCardStateUpdate(battleID, nextRevision, in current);

                cache.Cards[pair.Key] = current;

                Logging.Info(
                    LogCategory.Presentation,
                    $"[CARD STATE UPDATE EXPORT] "
                        + $"Battle={battleID} | "
                        + $"Revision={nextRevision} | "
                        + $"Card={current.RuntimeID} | "
                        + $"Zone={current.Location.Zone}"
                );
            }
        }

        private void ExportHandChanges(
            BattlePresentationBridge bridge,
            ulong battleID,
            RefRW<PresentationStateRevision> revision,
            BattleProjectionCache cache
        )
        {
            foreach (KeyValuePair<BattleSide, HandPresentationState> pair in currentHands)
            {
                HandPresentationState current = pair.Value;

                if (
                    cache.Hands.TryGetValue(pair.Key, out HandPresentationState previous)
                    && EqualityComparer<HandPresentationState>.Default.Equals(previous, current)
                )
                {
                    continue;
                }

                ulong nextRevision = AllocateNextRevision(revision);

                bridge.PublishHandStateUpdate(battleID, nextRevision, in current);

                cache.Hands[pair.Key] = current;
            }
        }

        private void ExportPlayerChanges(
            BattlePresentationBridge bridge,
            ulong battleID,
            RefRW<PresentationStateRevision> revision,
            BattleProjectionCache cache
        )
        {
            foreach (KeyValuePair<BattleSide, PlayerPresentationState> pair in currentPlayers)
            {
                PlayerPresentationState current = pair.Value;

                if (
                    cache.Players.TryGetValue(pair.Key, out PlayerPresentationState previous)
                    && EqualityComparer<PlayerPresentationState>.Default.Equals(previous, current)
                )
                {
                    continue;
                }

                ulong nextRevision = AllocateNextRevision(revision);

                bridge.PublishPlayerStateUpdate(battleID, nextRevision, in current);

                cache.Players[pair.Key] = current;
            }
        }

        private bool TryBuildCharacterState(Entity character, out CharacterPresentationState state)
        {
            state = default;

            if (
                !EntityManager.Exists(character)
                || !EntityManager.HasComponent<CardRuntimeID>(character)
                || !EntityManager.HasComponent<CharacterStats>(character)
                || !EntityManager.HasComponent<CurrentHealth>(character)
            )
            {
                return false;
            }

            CardRuntimeID runtimeID = EntityManager.GetComponentData<CardRuntimeID>(character);
            CharacterStats stats = EntityManager.GetComponentData<CharacterStats>(character);
            CurrentHealth health = EntityManager.GetComponentData<CurrentHealth>(character);

            state = PresentationStateBuilder.BuildCharacter(in runtimeID, in stats, in health);

            return true;
        }

        private bool TryAddCurrentCard(
            Entity card,
            BattleSide side,
            CardPresentationLocation location
        )
        {
            if (
                !EntityManager.Exists(card)
                || !EntityManager.HasComponent<CardRuntimeID>(card)
                || !EntityManager.HasComponent<CardDefinitionID>(card)
            )
            {
                return false;
            }

            CardRuntimeID runtimeID = EntityManager.GetComponentData<CardRuntimeID>(card);

            CardDefinitionID definitionID = EntityManager.GetComponentData<CardDefinitionID>(card);

            RuntimeCardType cardType;

            if (EntityManager.HasComponent<CharacterTag>(card))
            {
                cardType = RuntimeCardType.Character;
            }
            else
            {
                // Once SkillTag exists, explicitly validate it here.
                return false;
            }

            CardPresentationState cardState = PresentationStateBuilder.BuildCard(
                in runtimeID,
                in definitionID,
                cardType,
                side,
                in location
            );

            /*
             * This is also an invariant check:
             * one runtime card cannot be in two zones.
             */
            if (!currentCards.TryAdd(runtimeID.Value, cardState))
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"Card RuntimeID={runtimeID.Value} appeared "
                        + "in more than one authoritative zone."
                );

                return false;
            }

            if (cardType == RuntimeCardType.Character)
            {
                if (!TryBuildCharacterState(card, out CharacterPresentationState characterState))
                {
                    return false;
                }

                if (!currentCharacters.TryAdd(runtimeID.Value, characterState))
                {
                    Logging.Error(
                        LogCategory.Presentation,
                        $"Duplicate character RuntimeID={runtimeID.Value}."
                    );

                    return false;
                }
            }

            return true;
        }

        private bool TryBuildCurrentProjection(Entity battle)
        {
            currentCards.Clear();
            currentCharacters.Clear();
            currentHands.Clear();
            currentPlayers.Clear();

            using NativeArray<Entity> players = playerQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < players.Length; i++)
            {
                Entity player = players[i];

                OwnedBattle ownedBattle = EntityManager.GetComponentData<OwnedBattle>(player);

                if (ownedBattle.Battle != battle)
                    continue;

                Team team = EntityManager.GetComponentData<Team>(player);

                RemainingActionPoints remainingActionPoints =
                    EntityManager.GetComponentData<RemainingActionPoints>(player);

                PlayerPresentationState playerState = PresentationStateBuilder.BuildPlayer(
                    in team,
                    in remainingActionPoints
                );

                if (!currentPlayers.TryAdd(team.Side, playerState))
                {
                    Logging.Error(
                        LogCategory.Presentation,
                        $"Duplicate player projection for Side={team.Side}."
                    );

                    return false;
                }

                MaxHandSize maxHandSize = EntityManager.GetComponentData<MaxHandSize>(player);

                HandPresentationState handState = PresentationStateBuilder.BuildHand(
                    in team,
                    in maxHandSize
                );

                if (!currentHands.TryAdd(team.Side, handState))
                {
                    Logging.Error(
                        LogCategory.Presentation,
                        $"Duplicate hand projection for Side={team.Side}."
                    );

                    return false;
                }

                DynamicBuffer<DeckCard> deck = EntityManager.GetBuffer<DeckCard>(player);

                DynamicBuffer<HandCard> hand = EntityManager.GetBuffer<HandCard>(player);

                DynamicBuffer<FieldCard> field = EntityManager.GetBuffer<FieldCard>(player);

                for (int j = 0; j < deck.Length; j++)
                {
                    if (
                        !TryAddCurrentCard(deck[j].Card, team.Side, CardPresentationLocation.Deck())
                    )
                    {
                        return false;
                    }
                }

                for (int j = 0; j < hand.Length; j++)
                {
                    if (
                        !TryAddCurrentCard(
                            hand[j].Card,
                            team.Side,
                            CardPresentationLocation.Hand(hand[j].Position)
                        )
                    )
                    {
                        return false;
                    }
                }

                for (int j = 0; j < field.Length; j++)
                {
                    if (
                        !TryAddCurrentCard(
                            field[j].Card,
                            team.Side,
                            CardPresentationLocation.Field(field[j].Position)
                        )
                    )
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static ulong AllocateNextRevision(RefRW<PresentationStateRevision> revision)
        {
            ulong nextRevision = revision.ValueRO.Value + 1;

            revision.ValueRW.Value = nextRevision;

            return nextRevision;
        }
    }
}
