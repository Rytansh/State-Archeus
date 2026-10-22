using System.Collections.Generic;
using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Setup
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleInitialisationGroup))]
    [UpdateAfter(typeof(RosterInitialisationSystem))]
    public partial struct BattleReadinessSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RosterReadyRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityManager entities = state.EntityManager;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Record outcomes and log them after successful ECB playback.
            // That way we don't announce success before the handoff exists.
            var outcomes = new List<ReadinessOutcome>();

            // Count requests first. If a battle has two readiness requests,
            // neither request is allowed to advance to Initiative selection.
            var requestsPerBattle = new Dictionary<Entity, int>();
            foreach (
                RefRO<RosterReadyRequest> request in SystemAPI.Query<RefRO<RosterReadyRequest>>()
            )
            {
                Entity battle = request.ValueRO.Battle;
                requestsPerBattle.TryGetValue(battle, out int count);
                requestsPerBattle[battle] = count + 1;
            }

            foreach (
                var (request, requestEntity) in SystemAPI
                    .Query<RefRO<RosterReadyRequest>>()
                    .WithEntityAccess()
            )
            {
                Entity battle = request.ValueRO.Battle;
                BattleReadinessFailureReason reason;

                Logging.Info(
                    LogCategory.Setup,
                    $"[BattleReadiness] Checking Battle={DescribeBattle(entities, battle)}, "
                        + $"Request={requestEntity.Index}:{requestEntity.Version}"
                );

                if (requestsPerBattle[battle] != 1)
                {
                    reason = BattleReadinessFailureReason.DuplicateReadinessRequest;
                }
                else if (
                    !entities.HasBuffer<BattlePlayerEntry>(requestEntity)
                    || !entities.HasBuffer<PlayerDeckEntry>(requestEntity)
                    || entities.HasComponent<InitialRosterConstructionRequest>(requestEntity)
                    || entities.HasComponent<RosterInitialisationFailed>(requestEntity)
                    || entities.HasComponent<BattleReadinessFailed>(requestEntity)
                    || entities.HasComponent<BattleStartRequest>(requestEntity)
                )
                {
                    reason = BattleReadinessFailureReason.InvalidSetupRequest;
                }
                else if (
                    ValidateBattle(
                        entities,
                        battle,
                        entities.GetBuffer<BattlePlayerEntry>(requestEntity),
                        entities.GetBuffer<PlayerDeckEntry>(requestEntity),
                        out reason
                    )
                )
                {
                    ecb.RemoveComponent<RosterReadyRequest>(requestEntity);
                    ecb.AddComponent(requestEntity, new BattleStartRequest { Battle = battle });
                    outcomes.Add(
                        new ReadinessOutcome
                        {
                            Battle = battle,
                            Request = requestEntity,
                            Success = true,
                            PlayerCount = entities
                                .GetBuffer<BattlePlayerEntry>(requestEntity)
                                .Length,
                            DeckCount = entities.GetBuffer<PlayerDeckEntry>(requestEntity).Length,
                        }
                    );
                    continue;
                }

                ecb.RemoveComponent<RosterReadyRequest>(requestEntity);

                // A malformed setup entity must not retain another executable
                // stage request that could bypass this readiness failure.
                if (entities.HasComponent<InitialRosterConstructionRequest>(requestEntity))
                    ecb.RemoveComponent<InitialRosterConstructionRequest>(requestEntity);
                if (entities.HasComponent<BattleStartRequest>(requestEntity))
                    ecb.RemoveComponent<BattleStartRequest>(requestEntity);

                var failure = new BattleReadinessFailed { Battle = battle, Reason = reason };
                if (entities.HasComponent<BattleReadinessFailed>(requestEntity))
                    ecb.SetComponent(requestEntity, failure);
                else
                    ecb.AddComponent(requestEntity, failure);

                outcomes.Add(
                    new ReadinessOutcome
                    {
                        Battle = battle,
                        Request = requestEntity,
                        Success = false,
                        FailureReason = reason,
                    }
                );
            }

            // The next stage must update AFTER this playback.
            ecb.Playback(entities);

            foreach (ReadinessOutcome outcome in outcomes)
            {
                string location =
                    $"Battle={DescribeBattle(entities, outcome.Battle)}, "
                    + $"Request={outcome.Request.Index}:{outcome.Request.Version}";

                if (outcome.Success)
                {
                    Logging.Info(
                        LogCategory.Setup,
                        $"[BattleReadiness] PASSED | {location} | "
                            + $"Players={outcome.PlayerCount}, DeckCards={outcome.DeckCount} | "
                            + "Next=BattleStartRequest"
                    );
                }
                else
                {
                    Logging.Error(
                        LogCategory.Setup,
                        $"[BattleReadiness] FAILED | {location} | "
                            + $"Reason={outcome.FailureReason} | "
                            + "Battle start blocked"
                    );
                }
            }
        }

        private struct ReadinessOutcome
        {
            public Entity Battle;
            public Entity Request;
            public bool Success;
            public BattleReadinessFailureReason FailureReason;
            public int PlayerCount;
            public int DeckCount;
        }

        private static string DescribeBattle(EntityManager entities, Entity battle)
        {
            string label = $"{battle.Index}:{battle.Version}";
            if (entities.Exists(battle) && entities.HasComponent<BattleID>(battle))
                label += $" (BattleID={entities.GetComponentData<BattleID>(battle).Value})";

            return label;
        }

        private static bool ValidateBattle(
            EntityManager entities,
            Entity battle,
            DynamicBuffer<BattlePlayerEntry> expectedPlayers,
            DynamicBuffer<PlayerDeckEntry> expectedDeck,
            out BattleReadinessFailureReason reason
        )
        {
            reason = BattleReadinessFailureReason.None;

            if (
                !entities.Exists(battle)
                || !entities.HasComponent<BattleTag>(battle)
                || !entities.HasComponent<BattleState>(battle)
                || !entities.HasComponent<BattleRuntimeIDCounter>(battle)
                || !entities.HasComponent<BattleContentRegistry>(battle)
                || !entities.HasBuffer<BattlePlayerReference>(battle)
                || !entities.HasBuffer<BattleParticipant>(battle)
            )
            {
                reason = BattleReadinessFailureReason.InvalidBattle;
                return false;
            }

            if (
                entities.GetComponentData<BattleState>(battle).Phase != BattlePhase.Setup
                || !entities
                    .GetComponentData<BattleContentRegistry>(battle)
                    .BattleRegistryReference.IsCreated
            )
            {
                reason = BattleReadinessFailureReason.InvalidBattle;
                return false;
            }

            // Current BattleCreationSystem contract: exactly two players.
            // Change this when the supported battle format changes.
            DynamicBuffer<BattlePlayerReference> playerReferences =
                entities.GetBuffer<BattlePlayerReference>(battle);
            if (expectedPlayers.Length != 2 || playerReferences.Length != expectedPlayers.Length)
            {
                reason = BattleReadinessFailureReason.PlayerMismatch;
                return false;
            }

            var expectedPlayerIDs = new HashSet<ulong>();
            foreach (BattlePlayerEntry entry in expectedPlayers)
            {
                if (!expectedPlayerIDs.Add(entry.PlayerID))
                {
                    reason = BattleReadinessFailureReason.PlayerMismatch;
                    return false;
                }
            }

            var seenPlayers = new HashSet<Entity>();
            var seenCards = new HashSet<Entity>();
            var seenRuntimeIDs = new HashSet<uint>();
            var leads = new HashSet<Entity>();
            var matchedDeckEntries = new bool[expectedDeck.Length];
            var observedSides = new HashSet<BattleSide>();
            uint nextRuntimeID = entities.GetComponentData<BattleRuntimeIDCounter>(battle).NextID;

            foreach (BattlePlayerReference reference in playerReferences)
            {
                Entity player = reference.Player;
                if (
                    !entities.Exists(player)
                    || !seenPlayers.Add(player)
                    || !entities.HasComponent<PlayerTag>(player)
                    || !entities.HasComponent<BattlePlayerIdentity>(player)
                    || !entities.HasComponent<OwnedBattle>(player)
                    || !entities.HasComponent<BattleSideAssignment>(player)
                    || !entities.HasComponent<LeadCharacter>(player)
                    || !entities.HasBuffer<FieldCard>(player)
                    || !entities.HasBuffer<DeckCard>(player)
                    || !entities.HasBuffer<HandCard>(player)
                )
                {
                    reason = BattleReadinessFailureReason.PlayerMismatch;
                    return false;
                }

                if (entities.GetComponentData<OwnedBattle>(player).Battle != battle)
                {
                    reason = BattleReadinessFailureReason.PlayerMismatch;
                    return false;
                }

                ulong playerID = entities.GetComponentData<BattlePlayerIdentity>(player).PlayerID;
                if (!TryFindExpectedLead(expectedPlayers, playerID, out uint leadDefinitionID))
                {
                    reason = BattleReadinessFailureReason.PlayerMismatch;
                    return false;
                }

                BattleSide playerSide = entities
                    .GetComponentData<BattleSideAssignment>(player)
                    .Side;
                if (
                    (playerSide != BattleSide.SideA && playerSide != BattleSide.SideB)
                    || !observedSides.Add(playerSide)
                )
                {
                    // Two opposing players must occupy distinct sides.
                    reason = BattleReadinessFailureReason.PlayerMismatch;
                    return false;
                }

                Entity lead = entities.GetComponentData<LeadCharacter>(player).Value;
                DynamicBuffer<FieldCard> field = entities.GetBuffer<FieldCard>(player);
                if (
                    field.Length != 1
                    || field[0].Position != FieldPosition.Lead
                    || field[0].Card != lead
                    || !ValidateCard(
                        entities,
                        lead,
                        battle,
                        player,
                        playerSide,
                        leadDefinitionID,
                        RuntimeCardType.Character,
                        nextRuntimeID,
                        seenCards,
                        seenRuntimeIDs
                    )
                )
                {
                    reason = BattleReadinessFailureReason.LeadMismatch;
                    return false;
                }
                leads.Add(lead);

                // Cards haven't been drawn yet; the only field card is the Lead.
                if (entities.GetBuffer<HandCard>(player).Length != 0)
                {
                    reason = BattleReadinessFailureReason.DeckMismatch;
                    return false;
                }

                DynamicBuffer<DeckCard> deck = entities.GetBuffer<DeckCard>(player);
                int expectedCount = 0;
                for (int i = 0; i < expectedDeck.Length; i++)
                {
                    if (expectedDeck[i].PlayerID == playerID)
                        expectedCount++;
                }
                if (deck.Length != expectedCount)
                {
                    reason = BattleReadinessFailureReason.DeckMismatch;
                    return false;
                }

                foreach (DeckCard deckEntry in deck)
                {
                    Entity card = deckEntry.Card;
                    if (!entities.Exists(card) || !entities.HasComponent<CardDefinitionID>(card))
                    {
                        reason = BattleReadinessFailureReason.DeckMismatch;
                        return false;
                    }

                    uint actualDefinitionID = entities
                        .GetComponentData<CardDefinitionID>(card)
                        .Value;
                    bool isCharacter = entities.HasComponent<CharacterTag>(card);
                    bool isSkill = entities.HasComponent<SkillTag>(card);
                    if (isCharacter == isSkill)
                    {
                        reason = BattleReadinessFailureReason.DeckMismatch;
                        return false;
                    }
                    RuntimeCardType actualType = isCharacter
                        ? RuntimeCardType.Character
                        : RuntimeCardType.Skill;

                    // Multiset comparison: repeated definition IDs are supported,
                    // but each requested entry can match only one runtime card.
                    int matchedIndex = -1;
                    for (int i = 0; i < expectedDeck.Length; i++)
                    {
                        PlayerDeckEntry requested = expectedDeck[i];
                        if (
                            !matchedDeckEntries[i]
                            && requested.PlayerID == playerID
                            && requested.CardDefinitionID == actualDefinitionID
                            && requested.CardType == actualType
                        )
                        {
                            matchedIndex = i;
                            break;
                        }
                    }
                    if (
                        matchedIndex < 0
                        || !ValidateCard(
                            entities,
                            card,
                            battle,
                            player,
                            playerSide,
                            actualDefinitionID,
                            actualType,
                            nextRuntimeID,
                            seenCards,
                            seenRuntimeIDs
                        )
                    )
                    {
                        reason = BattleReadinessFailureReason.DeckMismatch;
                        return false;
                    }
                    matchedDeckEntries[matchedIndex] = true;
                }
            }

            for (int i = 0; i < matchedDeckEntries.Length; i++)
            {
                if (!matchedDeckEntries[i])
                {
                    reason = BattleReadinessFailureReason.DeckMismatch;
                    return false;
                }
            }

            // At initial readiness, exactly the Leads should participate.
            DynamicBuffer<BattleParticipant> participants = entities.GetBuffer<BattleParticipant>(
                battle
            );
            if (participants.Length != leads.Count)
            {
                reason = BattleReadinessFailureReason.ParticipantMismatch;
                return false;
            }
            var participantEntities = new HashSet<Entity>();
            foreach (BattleParticipant entry in participants)
            {
                if (
                    !leads.Contains(entry.Participant)
                    || !participantEntities.Add(entry.Participant)
                )
                {
                    reason = BattleReadinessFailureReason.ParticipantMismatch;
                    return false;
                }
            }

            // Detect runtime cards/players belonging to this battle but missing
            // from all registered player rosters. This protects against orphaned
            // entities produced by some other setup system.
            using var query = entities.CreateEntityQuery(ComponentType.ReadOnly<OwnedBattle>());
            using var ownedEntities = query.ToEntityArray(Allocator.Temp);
            foreach (Entity owned in ownedEntities)
            {
                if (entities.GetComponentData<OwnedBattle>(owned).Battle != battle)
                    continue;

                if (entities.HasComponent<PlayerTag>(owned) && !seenPlayers.Contains(owned))
                {
                    reason = BattleReadinessFailureReason.UnregisteredRuntimeEntity;
                    return false;
                }

                bool isCard =
                    entities.HasComponent<CharacterTag>(owned)
                    || entities.HasComponent<SkillTag>(owned)
                    || entities.HasComponent<CardRuntimeID>(owned);
                if (isCard && !seenCards.Contains(owned))
                {
                    reason = BattleReadinessFailureReason.UnregisteredRuntimeEntity;
                    return false;
                }
            }

            return true;
        }

        private static bool TryFindExpectedLead(
            DynamicBuffer<BattlePlayerEntry> expectedPlayers,
            ulong playerID,
            out uint leadDefinitionID
        )
        {
            foreach (BattlePlayerEntry entry in expectedPlayers)
            {
                if (entry.PlayerID == playerID)
                {
                    leadDefinitionID = entry.LeadCharacterDefinitionID;
                    return true;
                }
            }
            leadDefinitionID = default;
            return false;
        }

        private static bool ValidateCard(
            EntityManager entities,
            Entity card,
            Entity battle,
            Entity owner,
            BattleSide side,
            uint definitionID,
            RuntimeCardType type,
            uint nextRuntimeID,
            HashSet<Entity> seenCards,
            HashSet<uint> seenRuntimeIDs
        )
        {
            if (
                !entities.Exists(card)
                || !entities.HasComponent<CardDefinitionID>(card)
                || !entities.HasComponent<CardRuntimeID>(card)
                || !entities.HasComponent<CardOwner>(card)
                || !entities.HasComponent<OwnedBattle>(card)
                || !entities.HasComponent<BattleSideAssignment>(card)
            )
                return false;

            if (
                entities.GetComponentData<CardDefinitionID>(card).Value != definitionID
                || entities.GetComponentData<CardOwner>(card).Player != owner
                || entities.GetComponentData<OwnedBattle>(card).Battle != battle
                || entities.GetComponentData<BattleSideAssignment>(card).Side != side
            )
                return false;

            uint runtimeID = entities.GetComponentData<CardRuntimeID>(card).Value;
            if (runtimeID == 0 || runtimeID >= nextRuntimeID)
                return false;

            bool character = entities.HasComponent<CharacterTag>(card);
            bool skill = entities.HasComponent<SkillTag>(card);
            if (character == skill)
                return false;

            if (type == RuntimeCardType.Character)
            {
                if (
                    !character
                    || !entities.HasComponent<CharacterRuntimeStatBaseline>(card)
                    || !entities.HasComponent<CurrentHealth>(card)
                    || !entities.HasBuffer<BehaviourReference>(card)
                    || !entities.HasBuffer<BehaviourRuntimeState>(card)
                    || !entities.HasBuffer<ActiveEffect>(card)
                )
                    return false;

                if (
                    entities.GetBuffer<BehaviourReference>(card).Length
                    != entities.GetBuffer<BehaviourRuntimeState>(card).Length
                )
                    return false;

                CharacterRuntimeStatBaseline stats =
                    entities.GetComponentData<CharacterRuntimeStatBaseline>(card);
                float currentHealth = entities.GetComponentData<CurrentHealth>(card).Value;
                if (
                    !IsFinite(stats.Attack)
                    || !IsFinite(stats.Defense)
                    || !IsFinite(stats.MaxHealth)
                    || !IsFinite(stats.CritRATE)
                    || !IsFinite(stats.CritDMG)
                    || !IsFinite(currentHealth)
                    || stats.MaxHealth <= 0
                    || currentHealth <= 0
                    || currentHealth > stats.MaxHealth
                )
                    return false;
            }
            else if (type == RuntimeCardType.Skill)
            {
                if (!skill || !entities.HasComponent<SkillRuntimeStatBaseline>(card))
                    return false;

                SkillRuntimeStatBaseline stats =
                    entities.GetComponentData<SkillRuntimeStatBaseline>(card);
                if (!IsFinite(stats.Attack) || !IsFinite(stats.Defense) || !IsFinite(stats.Health))
                    return false;
            }
            else
            {
                return false;
            }

            // These sets span BOTH players and all zones.
            return seenCards.Add(card) && seenRuntimeIDs.Add(runtimeID);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
