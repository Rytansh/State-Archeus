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
    [UpdateAfter(typeof(PresentationUpdateExportSystem))]
    public partial class PresentationStateRecoverySystem : SystemBase
    {
        private readonly List<CharacterPresentationState> characters = new();
        private readonly List<CardPresentationState> cards = new();
        private readonly List<HandPresentationState> hands = new();
        private readonly List<PlayerPresentationState> playerStates = new();

        private readonly HashSet<BattleSide> projectedPlayerSides = new();
        private readonly HashSet<uint> projectedCardRuntimeIDs = new();
        private readonly HashSet<BattleSide> projectedHandSides = new();

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

            /*
             * One recovery attempt per simulation update.
             *
             * This prevents a permanently unavailable state
             * from causing a tight retry loop.
             */
            if (!bridge.TryTakeStateRecoveryRequest(out ulong requestedBattleID))
            {
                return;
            }

            bool battleFound = false;

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
                if (battleID.ValueRO.Value != requestedBattleID)
                {
                    continue;
                }

                battleFound = true;

                if (!TryBuildBattleProjection(battle))
                {
                    bridge.RequeueStateRecoveryRequest(requestedBattleID);

                    return;
                }

                /*
                 * Recovery is itself a new authoritative
                 * state publication.
                 */
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

                bridge.PublishRecoveryStateSnapshot(snapshot);

                Logging.Warn(
                    LogCategory.Presentation,
                    $"[STATE RECOVERY EXPORT] "
                        + $"Battle={requestedBattleID} | "
                        + $"Revision={nextRevision} | "
                        + $"Phase={projectedBattle.Phase} | "
                        + $"Turn={projectedBattle.TurnNumber} | "
                        + $"Characters={characters.Count} | "
                        + $"Cards={cards.Count} | "
                        + $"Hands={hands.Count}"
                );

                return;
            }

            /*
             * The authoritative battle no longer exists.
             * Do not requeue forever.
             */
            if (!battleFound)
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE RECOVERY EXPORT] "
                        + $"Recovery failed because "
                        + $"Battle={requestedBattleID} "
                        + $"does not exist."
                );
            }
        }

        private bool TryBuildBattleProjection(Entity battle)
        {
            characters.Clear();
            cards.Clear();
            hands.Clear();
            playerStates.Clear();

            projectedPlayerSides.Clear();
            projectedCardRuntimeIDs.Clear();
            projectedHandSides.Clear();

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
                        $"[STATE RECOVERY EXPORT] Duplicate player "
                            + $"projection for Side={team.Side}."
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
                    $"[STATE RECOVERY EXPORT] "
                        + $"Battle entity "
                        + $"{battle.Index} "
                        + "has no players available "
                        + "for presentation projection."
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
                    $"[STATE RECOVERY EXPORT] "
                        + $"Duplicate hand projection "
                        + $"for Side={team.Side}."
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
                    "[STATE RECOVERY EXPORT] "
                        + "Zone buffer references "
                        + "a card entity that "
                        + "does not exist."
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
                    $"[STATE RECOVERY EXPORT] "
                        + $"Card entity "
                        + $"{card.Index} "
                        + "is missing presentation "
                        + "identity components."
                );

                return false;
            }

            CardRuntimeID runtimeID = EntityManager.GetComponentData<CardRuntimeID>(card);

            /*
             * A runtime card must appear in exactly
             * one authoritative zone.
             */
            if (!projectedCardRuntimeIDs.Add(runtimeID.Value))
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE RECOVERY EXPORT] "
                        + $"Card RuntimeID="
                        + $"{runtimeID.Value} "
                        + "appears in more than one "
                        + "authoritative zone."
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
                 * Add SkillTag support here once
                 * runtime skill materialisation exists.
                 */
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE RECOVERY EXPORT] "
                        + $"Runtime card "
                        + $"{runtimeID.Value} "
                        + "has no supported "
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
             * Character state exists independently
             * of whether the card is in Deck,
             * Hand, or Field.
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
