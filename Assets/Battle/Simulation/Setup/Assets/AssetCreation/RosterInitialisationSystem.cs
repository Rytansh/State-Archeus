using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Content.Blobs;
using Archeus.Content.Lookup;
using Archeus.Content.Registries;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Setup
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleInitialisationGroup))]
    public partial struct RosterInitialisationSystem : ISystem
    {
        private struct ResolvedPlayer
        {
            public ulong PlayerID;
            public Entity Entity;
            public uint LeadDefinitionID;
            public BattleSideAssignment SideAssignment;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InitialRosterConstructionRequest>();
            state.RequireForUpdate<ContentLookupTables>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityManager entities = state.EntityManager;
            ContentLookupTables lookup = SystemAPI.GetSingleton<ContentLookupTables>();

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            using var scheduledBattles = new NativeList<Entity>(Allocator.Temp);

            foreach (
                var (request, players, cards, requestEntity) in SystemAPI
                    .Query<
                        RefRO<InitialRosterConstructionRequest>,
                        DynamicBuffer<BattlePlayerEntry>,
                        DynamicBuffer<PlayerDeckEntry>
                    >()
                    .WithEntityAccess()
            )
            {
                Entity battle = request.ValueRO.Battle;

                if (ContainsBattle(scheduledBattles, battle) || !IsBattleValid(entities, battle))
                {
                    RejectRequest(ecb, requestEntity, battle);
                    continue;
                }

                BattleContentRegistry content = entities.GetComponentData<BattleContentRegistry>(
                    battle
                );

                ref ContentBlobRegistry registry = ref content.BattleRegistryReference.Value;

                using var resolvedPlayers = new NativeArray<ResolvedPlayer>(
                    players.Length,
                    Allocator.Temp
                );

                if (
                    !TryResolvePlayers(entities, battle, players, resolvedPlayers)
                    || !ValidateDefinitions(in lookup, ref registry, resolvedPlayers, cards)
                )
                {
                    RejectRequest(ecb, requestEntity, battle);
                    continue;
                }

                BattleRuntimeIDCounter counter = entities.GetComponentData<BattleRuntimeIDCounter>(
                    battle
                );

                ulong requiredIDs = (ulong)players.Length + (ulong)cards.Length;

                if ((ulong)counter.NextID + requiredIDs > uint.MaxValue)
                {
                    RejectRequest(ecb, requestEntity, battle);
                    continue;
                }

                scheduledBattles.Add(battle);

                ConstructRoster(
                    ecb,
                    battle,
                    resolvedPlayers,
                    cards,
                    in lookup,
                    ref registry,
                    counter.NextID,
                    out uint nextRuntimeID
                );

                ecb.SetComponent(battle, new BattleRuntimeIDCounter { NextID = nextRuntimeID });
                ecb.RemoveComponent<InitialRosterConstructionRequest>(requestEntity);
                ecb.AddComponent(requestEntity, new RosterReadyRequest { Battle = battle });
            }

            ecb.Playback(entities);
        }

        private static bool IsBattleValid(EntityManager entities, Entity battle)
        {
            if (!entities.Exists(battle))
                return false;

            if (!entities.HasComponent<BattleTag>(battle))
                return false;

            if (!entities.HasComponent<BattleContentRegistry>(battle))
                return false;

            if (!entities.HasComponent<BattleRuntimeIDCounter>(battle))
                return false;

            if (!entities.HasBuffer<BattlePlayerReference>(battle))
                return false;

            BattleContentRegistry content = entities.GetComponentData<BattleContentRegistry>(
                battle
            );

            return content.BattleRegistryReference.IsCreated;
        }

        private static bool TryResolvePlayers(
            EntityManager entities,
            Entity battle,
            DynamicBuffer<BattlePlayerEntry> requestedPlayers,
            NativeArray<ResolvedPlayer> resolvedPlayers
        )
        {
            DynamicBuffer<BattlePlayerReference> references =
                entities.GetBuffer<BattlePlayerReference>(battle);

            if (requestedPlayers.Length == 0)
                return false;

            if (requestedPlayers.Length != references.Length)
                return false;

            for (int i = 0; i < requestedPlayers.Length; i++)
            {
                BattlePlayerEntry entry = requestedPlayers[i];

                for (int j = 0; j < i; j++)
                {
                    if (requestedPlayers[j].PlayerID == entry.PlayerID)
                        return false;
                }

                Entity matchingPlayer = Entity.Null;
                int matches = 0;

                foreach (BattlePlayerReference reference in references)
                {
                    Entity candidate = reference.Player;

                    if (!entities.Exists(candidate))
                        return false;

                    if (
                        !entities.HasComponent<PlayerTag>(candidate)
                        || !entities.HasComponent<BattlePlayerIdentity>(candidate)
                        || !entities.HasComponent<OwnedBattle>(candidate)
                        || !entities.HasComponent<BattleSideAssignment>(candidate)
                        || !entities.HasComponent<LeadCharacter>(candidate)
                        || !entities.HasBuffer<DeckCard>(candidate)
                        || !entities.HasBuffer<FieldCard>(candidate)
                    )
                    {
                        return false;
                    }

                    if (entities.GetComponentData<OwnedBattle>(candidate).Battle != battle)
                    {
                        return false;
                    }

                    ulong candidateID = entities
                        .GetComponentData<BattlePlayerIdentity>(candidate)
                        .PlayerID;

                    if (candidateID != entry.PlayerID)
                        continue;

                    matchingPlayer = candidate;
                    matches++;
                }

                if (matches != 1)
                    return false;

                // Reject reinitialisation of an existing roster.
                if (entities.GetComponentData<LeadCharacter>(matchingPlayer).Value != Entity.Null)
                {
                    return false;
                }

                if (entities.GetBuffer<DeckCard>(matchingPlayer).Length != 0)
                {
                    return false;
                }

                if (entities.GetBuffer<FieldCard>(matchingPlayer).Length != 0)
                {
                    return false;
                }

                resolvedPlayers[i] = new ResolvedPlayer
                {
                    PlayerID = entry.PlayerID,
                    Entity = matchingPlayer,
                    LeadDefinitionID = entry.LeadCharacterDefinitionID,
                    SideAssignment = entities.GetComponentData<BattleSideAssignment>(
                        matchingPlayer
                    ),
                };
            }

            SortPlayersByID(resolvedPlayers);

            return true;
        }

        private static void SortPlayersByID(NativeArray<ResolvedPlayer> players)
        {
            for (int i = 1; i < players.Length; i++)
            {
                ResolvedPlayer current = players[i];
                int j = i - 1;

                while (j >= 0 && players[j].PlayerID > current.PlayerID)
                {
                    players[j + 1] = players[j];
                    j--;
                }

                players[j + 1] = current;
            }
        }

        private static bool ValidateDefinitions(
            in ContentLookupTables lookup,
            ref ContentBlobRegistry registry,
            NativeArray<ResolvedPlayer> players,
            DynamicBuffer<PlayerDeckEntry> cards
        )
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (
                    !TryGetCharacterIndex(
                        in lookup,
                        ref registry,
                        players[i].LeadDefinitionID,
                        out _
                    )
                )
                {
                    return false;
                }
            }

            foreach (PlayerDeckEntry card in cards)
            {
                bool ownerFound = false;

                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i].PlayerID == card.PlayerID)
                    {
                        ownerFound = true;
                        break;
                    }
                }

                if (!ownerFound)
                    return false;

                switch (card.CardType)
                {
                    case RuntimeCardType.Character:
                        if (
                            !TryGetCharacterIndex(
                                in lookup,
                                ref registry,
                                card.CardDefinitionID,
                                out _
                            )
                        )
                        {
                            return false;
                        }
                        break;

                    case RuntimeCardType.Skill:
                        if (
                            !TryGetSkillIndex(in lookup, ref registry, card.CardDefinitionID, out _)
                        )
                        {
                            return false;
                        }
                        break;

                    default:
                        // Don't silently ignore unknown card types.
                        return false;
                }
            }

            return true;
        }

        private static bool TryGetCharacterIndex(
            in ContentLookupTables lookup,
            ref ContentBlobRegistry registry,
            uint definitionID,
            out int index
        )
        {
            index = -1;

            if (!lookup.CharacterIDToIndex.TryGetValue(definitionID, out int resolvedIndex))
            {
                return false;
            }

            if (resolvedIndex < 0 || resolvedIndex >= registry.Characters.Length)
            {
                return false;
            }

            index = resolvedIndex;
            return true;
        }

        private static bool TryGetSkillIndex(
            in ContentLookupTables lookup,
            ref ContentBlobRegistry registry,
            uint definitionID,
            out int index
        )
        {
            index = -1;

            // Match this name to your actual Skill lookup map.
            if (!lookup.SkillIDToIndex.TryGetValue(definitionID, out int resolvedIndex))
            {
                return false;
            }

            if (resolvedIndex < 0 || resolvedIndex >= registry.Skills.Length)
            {
                return false;
            }

            index = resolvedIndex;
            return true;
        }

        private static void ConstructRoster(
            EntityCommandBuffer ecb,
            Entity battle,
            NativeArray<ResolvedPlayer> players,
            DynamicBuffer<PlayerDeckEntry> cards,
            in ContentLookupTables lookup,
            ref ContentBlobRegistry registry,
            uint initialRuntimeID,
            out uint nextRuntimeID
        )
        {
            nextRuntimeID = initialRuntimeID;

            // Initialise all player's lead characters
            for (int i = 0; i < players.Length; i++)
            {
                ResolvedPlayer player = players[i];

                TryGetCharacterIndex(
                    in lookup,
                    ref registry,
                    player.LeadDefinitionID,
                    out int index
                );

                ref CharacterDefinitionBlob definition = ref registry.Characters[index];

                var components = new CardEntityComponents
                {
                    Battle = battle,
                    Owner = player.Entity,
                    DefinitionID = player.LeadDefinitionID,
                    RuntimeID = nextRuntimeID++,
                    SideAssignment = player.SideAssignment,
                };

                Entity lead = CardEntityBuilder.CreateCharacterEntity(
                    ecb,
                    in components,
                    ref definition
                );

                BattleParticipant leadParticipant = new BattleParticipant { Participant = lead };

                ecb.SetComponent(player.Entity, new LeadCharacter { Value = lead });
                ecb.AppendToBuffer(
                    player.Entity,
                    new FieldCard { Card = lead, Position = FieldPosition.Lead }
                );
                ecb.AppendToBuffer(battle, leadParticipant);
            }

            //Initialise the rest of the deck
            for (int i = 0; i < players.Length; i++)
            {
                ResolvedPlayer player = players[i];

                foreach (PlayerDeckEntry entry in cards)
                {
                    if (entry.PlayerID != player.PlayerID)
                        continue;

                    var components = new CardEntityComponents
                    {
                        Battle = battle,
                        Owner = player.Entity,
                        DefinitionID = entry.CardDefinitionID,
                        RuntimeID = nextRuntimeID++,
                        SideAssignment = player.SideAssignment,
                    };

                    Entity cardEntity;

                    switch (entry.CardType)
                    {
                        case RuntimeCardType.Character:
                        {
                            TryGetCharacterIndex(
                                in lookup,
                                ref registry,
                                entry.CardDefinitionID,
                                out int index
                            );

                            ref CharacterDefinitionBlob definition = ref registry.Characters[index];

                            cardEntity = CardEntityBuilder.CreateCharacterEntity(
                                ecb,
                                in components,
                                ref definition
                            );
                            break;
                        }

                        case RuntimeCardType.Skill:
                        {
                            TryGetSkillIndex(
                                in lookup,
                                ref registry,
                                entry.CardDefinitionID,
                                out int index
                            );

                            ref SkillDefinitionBlob definition = ref registry.Skills[index];

                            cardEntity = CardEntityBuilder.CreateSkillEntity(
                                ecb,
                                in components,
                                ref definition
                            );
                            break;
                        }

                        default:
                            // Already excluded by preflight validation.
                            continue;
                    }

                    ecb.AppendToBuffer(player.Entity, new DeckCard { Card = cardEntity });
                }
            }
        }

        private static bool ContainsBattle(NativeList<Entity> battles, Entity battle)
        {
            for (int i = 0; i < battles.Length; i++)
            {
                if (battles[i] == battle)
                    return true;
            }

            return false;
        }

        private static void RejectRequest(
            EntityCommandBuffer ecb,
            Entity requestEntity,
            Entity battle
        )
        {
            ecb.RemoveComponent<InitialRosterConstructionRequest>(requestEntity);
            ecb.AddComponent(requestEntity, new RosterInitialisationFailed { Battle = battle });
        }
    }
}
