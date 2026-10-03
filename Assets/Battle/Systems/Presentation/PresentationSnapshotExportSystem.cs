using System.Collections.Generic;
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
    [UpdateAfter(typeof(PresentationFactExportSystem))]
    public partial class PresentationSnapshotExportSystem : SystemBase
    {
        private EntityQuery bridgeQuery;
        private EntityQuery playerQuery;

        /*
         * Scratch state used while building one battle's
         * complete projection.
         */
        private readonly List<CharacterPresentationState> characters = new();
        private readonly List<CardPresentationState> cards = new();
        private readonly List<HandPresentationState> hands = new();
        private readonly List<PlayerPresentationState> playerStates = new();

        /*
         * Validation sets.
         *
         * Card RuntimeID must appear in exactly one zone.
         * There must be exactly one hand per side.
         */
        private readonly HashSet<uint> projectedCardRuntimeIDs = new();
        private readonly HashSet<BattleSide> projectedHandSides = new();
        private readonly HashSet<BattleSide> projectedPlayerSides = new();

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

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (battleID, battleState, turnCounter, revision, battle) in SystemAPI
                    .Query<
                        RefRO<BattleID>,
                        RefRO<BattleState>,
                        RefRO<TurnCounter>,
                        RefRW<PresentationStateRevision>
                    >()
                    .WithAll<BattleTag>()
                    .WithNone<InitialPresentationStatePublishedTag>()
                    .WithEntityAccess()
            )
            {
                if (battleState.ValueRO.Phase < BattlePhase.BattleReady)
                {
                    continue;
                }

                if (!TryBuildBattleProjection(battle))
                {
                    continue;
                }

                ulong nextRevision = revision.ValueRO.Value + 1;

                revision.ValueRW.Value = nextRevision;

                BattlePresentationState projectedBattle = PresentationStateBuilder.BuildBattle(
                    battleID.ValueRO.Value,
                    in battleState.ValueRO,
                    in turnCounter.ValueRO
                );

                PresentationStateSnapshot snapshot = new PresentationStateSnapshot
                {
                    Revision = nextRevision,
                    Battle = projectedBattle,
                    Players = playerStates.ToArray(),
                    Characters = characters.ToArray(),
                    Cards = cards.ToArray(),
                    Hands = hands.ToArray(),
                };

                bridge.PublishStateSnapshot(snapshot);

                ecb.AddComponent<InitialPresentationStatePublishedTag>(battle);

                Logging.Info(
                    LogCategory.Presentation,
                    $"[STATE EXPORT] "
                        + $"Battle={projectedBattle.BattleRuntimeID} | "
                        + $"Revision={nextRevision} | "
                        + $"Phase={projectedBattle.Phase} | "
                        + $"Turn={projectedBattle.TurnNumber} | "
                        + $"Characters={characters.Count} | "
                        + $"Cards={cards.Count} | "
                        + $"Hands={hands.Count}"
                );
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private bool TryBuildBattleProjection(Entity battle)
        {
            characters.Clear();
            cards.Clear();
            hands.Clear();
            playerStates.Clear();

            projectedCardRuntimeIDs.Clear();
            projectedHandSides.Clear();
            projectedPlayerSides.Clear();

            using NativeArray<Entity> players = playerQuery.ToEntityArray(Allocator.Temp);

            int playersInBattle = 0;

            for (int i = 0; i < players.Length; i++)
            {
                Entity player = players[i];

                OwnedBattle ownedBattle = EntityManager.GetComponentData<OwnedBattle>(player);

                if (ownedBattle.Battle != battle)
                {
                    continue;
                }

                playersInBattle++;

                Team team = EntityManager.GetComponentData<Team>(player);

                RemainingActionPoints remainingActionPoints =
                    EntityManager.GetComponentData<RemainingActionPoints>(player);

                if (!projectedPlayerSides.Add(team.Side))
                {
                    Logging.Error(
                        LogCategory.Presentation,
                        $"[STATE EXPORT] Duplicate player projection " + $"for Side={team.Side}."
                    );

                    return false;
                }

                PlayerPresentationState playerState = PresentationStateBuilder.BuildPlayer(
                    in team,
                    in remainingActionPoints
                );

                playerStates.Add(playerState);

                MaxHandSize maxHandSize = EntityManager.GetComponentData<MaxHandSize>(player);

                if (!TryAddHandState(in team, in maxHandSize))
                {
                    return false;
                }

                DynamicBuffer<DeckCard> deck = EntityManager.GetBuffer<DeckCard>(player);

                DynamicBuffer<HandCard> hand = EntityManager.GetBuffer<HandCard>(player);

                DynamicBuffer<FieldCard> field = EntityManager.GetBuffer<FieldCard>(player);

                for (int j = 0; j < deck.Length; j++)
                {
                    if (!TryAddCardState(deck[j].Card, team.Side, CardPresentationLocation.Deck()))
                    {
                        return false;
                    }
                }

                for (int j = 0; j < hand.Length; j++)
                {
                    if (
                        !TryAddCardState(
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
                        !TryAddCardState(
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

            if (playersInBattle == 0)
            {
                Logging.Warn(
                    LogCategory.Presentation,
                    $"[STATE EXPORT] "
                        + $"Battle={battle.Index} has no players "
                        + "available for card-state projection."
                );

                return false;
            }

            return true;
        }

        private bool TryAddHandState(in Team team, in MaxHandSize maxHandSize)
        {
            if (!projectedHandSides.Add(team.Side))
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE EXPORT] Duplicate hand " + $"projection for Side={team.Side}."
                );

                return false;
            }

            HandPresentationState handState = PresentationStateBuilder.BuildHand(
                in team,
                in maxHandSize
            );

            hands.Add(handState);

            return true;
        }

        private bool TryAddCardState(
            Entity card,
            BattleSide side,
            CardPresentationLocation location
        )
        {
            if (!EntityManager.Exists(card))
            {
                Logging.Error(
                    LogCategory.Presentation,
                    "[STATE EXPORT] Zone buffer references "
                        + "a card entity that no longer exists."
                );

                return false;
            }

            if (
                !EntityManager.HasComponent<CardRuntimeID>(card)
                || !EntityManager.HasComponent<CardDefinitionID>(card)
            )
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE EXPORT] Card entity "
                        + $"{card.Index} is missing runtime "
                        + "presentation identity."
                );

                return false;
            }

            CardRuntimeID runtimeID = EntityManager.GetComponentData<CardRuntimeID>(card);

            /*
             * A card appearing twice means the authoritative
             * zone invariant has already been violated.
             */
            if (!projectedCardRuntimeIDs.Add(runtimeID.Value))
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE EXPORT] Card RuntimeID="
                        + $"{runtimeID.Value} appeared in "
                        + "more than one authoritative zone."
                );

                return false;
            }

            CardDefinitionID definitionID = EntityManager.GetComponentData<CardDefinitionID>(card);

            RuntimeCardType cardType;

            if (EntityManager.HasComponent<CharacterTag>(card))
            {
                cardType = RuntimeCardType.Character;
            }
            else
            {
                /*
                 * For now only characters are materialised.
                 *
                 * When SkillTag exists, add:
                 *
                 * else if (EntityManager.HasComponent<SkillTag>(card))
                 *     cardType = RuntimeCardType.Skill;
                 */
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE EXPORT] Runtime card "
                        + $"{runtimeID.Value} has no supported "
                        + "runtime card type."
                );

                return false;
            }

            CardPresentationState cardState = PresentationStateBuilder.BuildCard(
                in runtimeID,
                in definitionID,
                cardType,
                side,
                in location
            );

            cards.Add(cardState);

            /*
             * CharacterPresentationState describes the
             * character itself, independently of zone.
             *
             * Therefore a character remains projected while
             * in Deck, Hand, or Field.
             */
            if (cardType == RuntimeCardType.Character)
            {
                if (!TryBuildCharacterState(card, out CharacterPresentationState characterState))
                {
                    return false;
                }

                characters.Add(characterState);
            }

            return true;
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

            CurrentHealth currentHealth = EntityManager.GetComponentData<CurrentHealth>(character);

            state = PresentationStateBuilder.BuildCharacter(
                in runtimeID,
                in stats,
                in currentHealth
            );

            return true;
        }
    }
}
