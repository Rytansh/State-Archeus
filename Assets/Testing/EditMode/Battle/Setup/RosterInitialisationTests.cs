using System.Collections.Generic;
using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Systems.Setup;
using Archeus.Content.Blobs;
using Archeus.Content.Lookup;
using Archeus.Content.Registries;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Testing.Battle.Setup
{
    /// <summary>
    /// Edit Mode integration tests: runtime rosters, not just setup request handoffs.
    /// The fixture creates a real, tiny BlobAssetReference and lookup maps.
    /// </summary>
    public class RosterInitialisationTests
    {
        private const uint FirstRuntimeID = 100;
        private const uint LeadAID = 1;
        private const uint LeadBID = 2;
        private const uint DeckCharacterAID = 3;
        private const uint DeckCharacterBID = 4;
        private const uint DeckSkillAID = 101;
        private const uint DeckSkillBID = 102;

        private World world;
        private EntityManager entities;
        private SystemHandle rosterSystem;
        private BlobAssetReference<ContentBlobRegistry> registryReference;
        private ContentLookupTables lookupTables;

        private struct Scenario
        {
            public Entity Battle;
            public Entity Request;
            public Entity PlayerA;
            public Entity PlayerB;
        }

        [SetUp]
        public void SetUp()
        {
            world = new World("Roster Initialisation Tests");
            entities = world.EntityManager;
            rosterSystem = world.CreateSystem<RosterInitialisationSystem>();

            CreateContentFixture();
        }

        [TearDown]
        public void TearDown()
        {
            // Neither a BlobAssetReference nor a NativeHashMap stored inside
            // an IComponentData is automatically disposed with its ECS world.
            world?.Dispose();

            lookupTables.Dispose();

            if (registryReference.IsCreated)
                registryReference.Dispose();
        }

        // --------------------------------------------------------------
        // Success path
        // --------------------------------------------------------------

        [Test]
        public void ValidRoster_ConstructsLeadsAndMixedCharacterSkillDecks()
        {
            Scenario scenario = CreateScenario();

            RunRoster();

            Assert.That(Count<CharacterTag>(), Is.EqualTo(4));
            Assert.That(Count<SkillTag>(), Is.EqualTo(2));

            AssertPlayerRoster(
                scenario.PlayerA,
                scenario.Battle,
                BattleSide.SideA,
                LeadAID,
                DeckCharacterAID,
                DeckSkillAID
            );

            AssertPlayerRoster(
                scenario.PlayerB,
                scenario.Battle,
                BattleSide.SideB,
                LeadBID,
                DeckCharacterBID,
                DeckSkillBID
            );
        }

        [Test]
        public void ConstructedCards_CopyDefinitionStatsAndInitialiseBehaviourBuffers()
        {
            Scenario scenario = CreateScenario();
            RunRoster();

            Entity leadA = GetLead(scenario.PlayerA);
            CharacterRuntimeStatBaseline leadStats =
                entities.GetComponentData<CharacterRuntimeStatBaseline>(leadA);

            Assert.That(leadStats.Attack, Is.EqualTo(101f));
            Assert.That(leadStats.Defense, Is.EqualTo(201f));
            Assert.That(leadStats.MaxHealth, Is.EqualTo(1001f));
            Assert.That(entities.GetComponentData<CurrentHealth>(leadA).Value, Is.EqualTo(1001f));

            Assert.That(entities.GetBuffer<BehaviourReference>(leadA).Length, Is.Zero);
            Assert.That(entities.GetBuffer<BehaviourRuntimeState>(leadA).Length, Is.Zero);
            Assert.That(entities.GetBuffer<ActiveEffect>(leadA).Length, Is.Zero);

            Entity characterCard = GetDeckCard(scenario.PlayerA, 0);
            CharacterRuntimeStatBaseline deckStats =
                entities.GetComponentData<CharacterRuntimeStatBaseline>(characterCard);
            Assert.That(deckStats.Attack, Is.EqualTo(103f));
            Assert.That(
                entities.GetComponentData<CurrentHealth>(characterCard).Value,
                Is.EqualTo(1003f)
            );

            Entity skillCard = GetDeckCard(scenario.PlayerA, 1);
            SkillRuntimeStatBaseline skillStats =
                entities.GetComponentData<SkillRuntimeStatBaseline>(skillCard);
            Assert.That(skillStats.Attack, Is.EqualTo(301f));
            Assert.That(skillStats.Defense, Is.EqualTo(401f));
            Assert.That(skillStats.Health, Is.EqualTo(501f));
            Assert.That(entities.HasComponent<CharacterRuntimeStatBaseline>(skillCard), Is.False);
        }

        [Test]
        public void RuntimeIDs_AreUniqueSequentialAndAdvanceCounter()
        {
            Scenario scenario = CreateScenario();
            RunRoster();

            uint[] actual = GetOrderedRuntimeIDs(scenario);
            Assert.That(actual, Is.EqualTo(new uint[] { 100, 101, 102, 103, 104, 105 }));

            HashSet<uint> distinct = new HashSet<uint>(actual);
            Assert.That(distinct.Count, Is.EqualTo(6));
            Assert.That(
                entities.GetComponentData<BattleRuntimeIDCounter>(scenario.Battle).NextID,
                Is.EqualTo(106U)
            );
        }

        [Test]
        public void SuccessfulRoster_AdvancesRequestAndPreservesItsConfiguration()
        {
            Scenario scenario = CreateScenario();
            RunRoster();

            Assert.That(entities.Exists(scenario.Request), Is.True);
            Assert.That(
                entities.HasComponent<InitialRosterConstructionRequest>(scenario.Request),
                Is.False
            );
            Assert.That(entities.HasComponent<RosterReadyRequest>(scenario.Request), Is.True);
            Assert.That(
                entities.HasComponent<RosterInitialisationFailed>(scenario.Request),
                Is.False
            );
            Assert.That(
                entities.GetComponentData<RosterReadyRequest>(scenario.Request).Battle,
                Is.EqualTo(scenario.Battle)
            );

            DynamicBuffer<BattlePlayerEntry> players = entities.GetBuffer<BattlePlayerEntry>(
                scenario.Request
            );
            DynamicBuffer<PlayerDeckEntry> cards = entities.GetBuffer<PlayerDeckEntry>(
                scenario.Request
            );

            Assert.That(players.Length, Is.EqualTo(2));
            Assert.That(cards.Length, Is.EqualTo(4));
            Assert.That(players[0].PlayerID, Is.EqualTo(1001UL));
            Assert.That(players[1].PlayerID, Is.EqualTo(1002UL));
            Assert.That(cards[0].CardDefinitionID, Is.EqualTo(DeckCharacterAID));
            Assert.That(cards[1].CardDefinitionID, Is.EqualTo(DeckSkillAID));
            Assert.That(cards[2].CardDefinitionID, Is.EqualTo(DeckCharacterBID));
            Assert.That(cards[3].CardDefinitionID, Is.EqualTo(DeckSkillBID));
            Assert.That(
                entities.GetComponentData<BattleState>(scenario.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup)
            );
        }

        [Test]
        public void RepeatedUpdates_DoNotConstructAdditionalCards()
        {
            Scenario scenario = CreateScenario();
            RunRoster();
            RunRoster();
            RunRoster();

            Assert.That(Count<CharacterTag>(), Is.EqualTo(4));
            Assert.That(Count<SkillTag>(), Is.EqualTo(2));
            Assert.That(entities.GetBuffer<DeckCard>(scenario.PlayerA).Length, Is.EqualTo(2));
            Assert.That(entities.GetBuffer<DeckCard>(scenario.PlayerB).Length, Is.EqualTo(2));
            Assert.That(
                entities.GetComponentData<BattleRuntimeIDCounter>(scenario.Battle).NextID,
                Is.EqualTo(106U)
            );
            Assert.That(entities.HasComponent<RosterReadyRequest>(scenario.Request), Is.True);
        }

        [Test]
        public void Leads_AppearInBattleParticipantBuffer()
        {
            Scenario scenario = CreateScenario();
            RunRoster();

            DynamicBuffer<BattleParticipant> participants = entities.GetBuffer<BattleParticipant>(
                scenario.Battle
            );

            Assert.That(participants.Length, Is.EqualTo(2));
            Assert.That(
                new[] { participants[0].Participant, participants[1].Participant },
                Is.EquivalentTo(new[] { GetLead(scenario.PlayerA), GetLead(scenario.PlayerB) })
            );
        }

        // --------------------------------------------------------------
        // Reject invalid requests without making a partial roster
        // --------------------------------------------------------------

        [Test]
        public void UnknownSkillDefinition_RejectsEntireRoster()
        {
            Scenario scenario = CreateScenario();
            DynamicBuffer<PlayerDeckEntry> cards = entities.GetBuffer<PlayerDeckEntry>(
                scenario.Request
            );
            PlayerDeckEntry invalid = cards[1];
            invalid.CardDefinitionID = 999999;
            cards[1] = invalid;

            RunRoster();
            AssertRejectedWithoutConstruction(scenario);
        }

        [Test]
        public void UnknownLeadDefinition_RejectsEntireRoster()
        {
            Scenario scenario = CreateScenario();
            DynamicBuffer<BattlePlayerEntry> players = entities.GetBuffer<BattlePlayerEntry>(
                scenario.Request
            );
            BattlePlayerEntry invalid = players[0];
            invalid.LeadCharacterDefinitionID = 999999;
            players[0] = invalid;

            RunRoster();
            AssertRejectedWithoutConstruction(scenario);
        }

        [Test]
        public void UnknownCardOwner_RejectsEntireRoster()
        {
            Scenario scenario = CreateScenario();
            DynamicBuffer<PlayerDeckEntry> cards = entities.GetBuffer<PlayerDeckEntry>(
                scenario.Request
            );
            PlayerDeckEntry invalid = cards[3];
            invalid.PlayerID = 999999;
            cards[3] = invalid;

            RunRoster();
            AssertRejectedWithoutConstruction(scenario);
        }

        [Test]
        public void InvalidCardType_RejectsEntireRoster()
        {
            Scenario scenario = CreateScenario();
            DynamicBuffer<PlayerDeckEntry> cards = entities.GetBuffer<PlayerDeckEntry>(
                scenario.Request
            );
            PlayerDeckEntry invalid = cards[0];
            invalid.CardType = (RuntimeCardType)255;
            cards[0] = invalid;

            RunRoster();
            AssertRejectedWithoutConstruction(scenario);
        }

        [Test]
        public void MissingRuntimePlayerReference_RejectsEntireRoster()
        {
            Scenario scenario = CreateScenario();
            DynamicBuffer<BattlePlayerReference> references =
                entities.GetBuffer<BattlePlayerReference>(scenario.Battle);
            references.RemoveAt(1);

            RunRoster();
            AssertRejectedWithoutConstruction(scenario);
        }

        // --------------------------------------------------------------
        // Multi-battle and deterministic construction
        // --------------------------------------------------------------

        [Test]
        public void TwoBattles_ConstructIndependentRosters()
        {
            Scenario first = CreateScenario(42, 1001, 1002);
            Scenario second = CreateScenario(43, 2001, 2002);

            RunRoster();

            Assert.That(Count<CharacterTag>(), Is.EqualTo(8));
            Assert.That(Count<SkillTag>(), Is.EqualTo(4));

            AssertPlayerRoster(
                first.PlayerA,
                first.Battle,
                BattleSide.SideA,
                LeadAID,
                DeckCharacterAID,
                DeckSkillAID
            );
            AssertPlayerRoster(
                second.PlayerA,
                second.Battle,
                BattleSide.SideA,
                LeadAID,
                DeckCharacterAID,
                DeckSkillAID
            );

            Assert.That(GetLead(first.PlayerA), Is.Not.EqualTo(GetLead(second.PlayerA)));
            Assert.That(
                entities.GetComponentData<BattleRuntimeIDCounter>(first.Battle).NextID,
                Is.EqualTo(106U)
            );
            Assert.That(
                entities.GetComponentData<BattleRuntimeIDCounter>(second.Battle).NextID,
                Is.EqualTo(106U)
            );
            Assert.That(entities.HasComponent<RosterReadyRequest>(first.Request), Is.True);
            Assert.That(entities.HasComponent<RosterReadyRequest>(second.Request), Is.True);
        }

        [Test]
        public void ReversingPlayerEntries_DoesNotChangePerCardRuntimeIDs()
        {
            Scenario first = CreateScenario(42, 1001, 1002, reversePlayers: false);
            Scenario second = CreateScenario(43, 1001, 1002, reversePlayers: true);

            RunRoster();

            Assert.That(GetOrderedRuntimeIDs(first), Is.EqualTo(GetOrderedRuntimeIDs(second)));
        }

        [Test]
        public void TwoPendingRequestsForSameBattle_DoNotDuplicateRoster()
        {
            Scenario scenario = CreateScenario();
            Entity duplicate = CreateRosterRequest(
                scenario.Battle,
                1001,
                1002,
                reversePlayers: false
            );

            RunRoster();

            Assert.That(Count<CharacterTag>(), Is.EqualTo(4));
            Assert.That(Count<SkillTag>(), Is.EqualTo(2));
            Assert.That(
                entities.GetComponentData<BattleRuntimeIDCounter>(scenario.Battle).NextID,
                Is.EqualTo(106U)
            );

            int succeeded =
                (entities.HasComponent<RosterReadyRequest>(scenario.Request) ? 1 : 0)
                + (entities.HasComponent<RosterReadyRequest>(duplicate) ? 1 : 0);
            int rejected =
                (entities.HasComponent<RosterInitialisationFailed>(scenario.Request) ? 1 : 0)
                + (entities.HasComponent<RosterInitialisationFailed>(duplicate) ? 1 : 0);

            Assert.That(succeeded, Is.EqualTo(1));
            Assert.That(rejected, Is.EqualTo(1));
        }

        // --------------------------------------------------------------
        // Scenario creation
        // --------------------------------------------------------------

        private Scenario CreateScenario(
            ulong battleID = 42,
            ulong playerAID = 1001,
            ulong playerBID = 1002,
            bool reversePlayers = false
        )
        {
            Entity battle = entities.CreateEntity();
            entities.AddComponent<BattleTag>(battle);
            entities.AddComponentData(battle, new BattleID { Value = battleID });
            entities.AddComponentData(battle, new BattleState { Phase = BattlePhase.Setup });
            entities.AddComponentData(
                battle,
                new BattleRuntimeIDCounter { NextID = FirstRuntimeID }
            );
            entities.AddComponentData(
                battle,
                new BattleContentRegistry { BattleRegistryReference = registryReference }
            );

            entities.AddBuffer<BattleParticipant>(battle);

            Entity playerA = CreatePlayer(battle, playerAID, BattleSide.SideA);
            Entity playerB = CreatePlayer(battle, playerBID, BattleSide.SideB);
            // Add the reference buffer AFTER creating both players; otherwise
            // their structural changes invalidate an earlier DynamicBuffer handle.
            DynamicBuffer<BattlePlayerReference> references =
                entities.AddBuffer<BattlePlayerReference>(battle);
            references.Add(new BattlePlayerReference { Player = playerA });
            references.Add(new BattlePlayerReference { Player = playerB });

            return new Scenario
            {
                Battle = battle,
                PlayerA = playerA,
                PlayerB = playerB,
                Request = CreateRosterRequest(battle, playerAID, playerBID, reversePlayers),
            };
        }

        private Entity CreatePlayer(Entity battle, ulong playerID, BattleSide side)
        {
            Entity player = entities.CreateEntity();
            entities.AddComponent<PlayerTag>(player);
            entities.AddComponentData(player, new BattlePlayerIdentity { PlayerID = playerID });
            entities.AddComponentData(player, new OwnedBattle { Battle = battle });
            entities.AddComponentData(player, new BattleSideAssignment { Side = side });
            entities.AddComponentData(player, new LeadCharacter { Value = Entity.Null });
            entities.AddBuffer<DeckCard>(player);
            entities.AddBuffer<HandCard>(player);
            entities.AddBuffer<FieldCard>(player);
            return player;
        }

        private Entity CreateRosterRequest(
            Entity battle,
            ulong playerAID,
            ulong playerBID,
            bool reversePlayers
        )
        {
            Entity request = entities.CreateEntity();
            entities.AddComponentData(
                request,
                new InitialRosterConstructionRequest { Battle = battle }
            );

            DynamicBuffer<BattlePlayerEntry> players = entities.AddBuffer<BattlePlayerEntry>(
                request
            );
            BattlePlayerEntry a = new BattlePlayerEntry
            {
                PlayerID = playerAID,
                LeadCharacterDefinitionID = LeadAID,
            };
            BattlePlayerEntry b = new BattlePlayerEntry
            {
                PlayerID = playerBID,
                LeadCharacterDefinitionID = LeadBID,
            };
            if (reversePlayers)
            {
                players.Add(b);
                players.Add(a);
            }
            else
            {
                players.Add(a);
                players.Add(b);
            }

            DynamicBuffer<PlayerDeckEntry> cards = entities.AddBuffer<PlayerDeckEntry>(request);
            cards.Add(
                new PlayerDeckEntry
                {
                    PlayerID = playerAID,
                    CardDefinitionID = DeckCharacterAID,
                    CardType = RuntimeCardType.Character,
                }
            );
            cards.Add(
                new PlayerDeckEntry
                {
                    PlayerID = playerAID,
                    CardDefinitionID = DeckSkillAID,
                    CardType = RuntimeCardType.Skill,
                }
            );
            cards.Add(
                new PlayerDeckEntry
                {
                    PlayerID = playerBID,
                    CardDefinitionID = DeckCharacterBID,
                    CardType = RuntimeCardType.Character,
                }
            );
            cards.Add(
                new PlayerDeckEntry
                {
                    PlayerID = playerBID,
                    CardDefinitionID = DeckSkillBID,
                    CardType = RuntimeCardType.Skill,
                }
            );

            return request;
        }

        // --------------------------------------------------------------
        // Real, minimal content fixture
        // --------------------------------------------------------------

        private void CreateContentFixture()
        {
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref ContentBlobRegistry root = ref builder.ConstructRoot<ContentBlobRegistry>();

                BlobBuilderArray<CharacterDefinitionBlob> characters = builder.Allocate(
                    ref root.Characters,
                    4
                );
                for (int i = 0; i < characters.Length; i++)
                {
                    ref CharacterDefinitionBlob character = ref characters[i];
                    character.ID = (uint)(i + 1);
                    character.CharacterBlobBaseStats = new CharacterBlobBaseStats
                    {
                        Attack = 101 + i,
                        Defense = 201 + i,
                        MaxHealth = 1001 + i,
                        CritRATE = 10 + i,
                        CritDMG = 25 + i,
                    };
                    builder.Allocate(ref character.BehaviourIndices, 0);
                }

                BlobBuilderArray<SkillDefinitionBlob> skills = builder.Allocate(ref root.Skills, 2);
                for (int i = 0; i < skills.Length; i++)
                {
                    ref SkillDefinitionBlob skill = ref skills[i];
                    skill.ID = (uint)(101 + i);
                    skill.SkillBlobBaseStats = new SkillBlobBaseStats
                    {
                        Attack = 301 + i,
                        Defense = 401 + i,
                        Health = 501 + i,
                    };
                    builder.Allocate(ref skill.BehaviourIndices, 0);
                }

                // The builder does not dereference these collections,
                // but initialise them for a well-formed registry.
                builder.Allocate(ref root.Behaviours, 0);
                builder.Allocate(ref root.AbilityPrograms, 0);
                builder.Allocate(ref root.Effects, 0);

                registryReference = builder.CreateBlobAssetReference<ContentBlobRegistry>(
                    Allocator.Persistent
                );
            }

            lookupTables = new ContentLookupTables
            {
                CharacterIDToIndex = new NativeHashMap<uint, int>(4, Allocator.Persistent),
                SkillIDToIndex = new NativeHashMap<uint, int>(2, Allocator.Persistent),
                BehaviourIDToIndex = new NativeHashMap<uint, int>(1, Allocator.Persistent),
                EffectIDToIndex = new NativeHashMap<uint, int>(1, Allocator.Persistent),
            };
            for (int i = 0; i < 4; i++)
                Assert.That(lookupTables.CharacterIDToIndex.TryAdd((uint)(i + 1), i), Is.True);
            for (int i = 0; i < 2; i++)
                Assert.That(lookupTables.SkillIDToIndex.TryAdd((uint)(101 + i), i), Is.True);

            Entity lookupEntity = entities.CreateEntity();
            entities.AddComponentData(lookupEntity, lookupTables);
        }

        // --------------------------------------------------------------
        // Assertions/helpers
        // --------------------------------------------------------------

        private void RunRoster() => rosterSystem.Update(world.Unmanaged);

        private int Count<T>()
            where T : unmanaged, IComponentData
        {
            using var query = entities.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private Entity GetLead(Entity player) =>
            entities.GetComponentData<LeadCharacter>(player).Value;

        private Entity GetDeckCard(Entity player, int index) =>
            entities.GetBuffer<DeckCard>(player)[index].Card;

        private uint[] GetOrderedRuntimeIDs(Scenario scenario)
        {
            return new[]
            {
                entities.GetComponentData<CardRuntimeID>(GetLead(scenario.PlayerA)).Value,
                entities.GetComponentData<CardRuntimeID>(GetLead(scenario.PlayerB)).Value,
                entities.GetComponentData<CardRuntimeID>(GetDeckCard(scenario.PlayerA, 0)).Value,
                entities.GetComponentData<CardRuntimeID>(GetDeckCard(scenario.PlayerA, 1)).Value,
                entities.GetComponentData<CardRuntimeID>(GetDeckCard(scenario.PlayerB, 0)).Value,
                entities.GetComponentData<CardRuntimeID>(GetDeckCard(scenario.PlayerB, 1)).Value,
            };
        }

        private void AssertPlayerRoster(
            Entity player,
            Entity battle,
            BattleSide expectedSide,
            uint leadDefinition,
            uint characterDefinition,
            uint skillDefinition
        )
        {
            Entity lead = GetLead(player);
            Assert.That(entities.Exists(lead), Is.True);
            Assert.That(entities.HasComponent<CharacterTag>(lead), Is.True);
            AssertCardIdentity(lead, battle, player, expectedSide, leadDefinition);

            DynamicBuffer<FieldCard> field = entities.GetBuffer<FieldCard>(player);
            Assert.That(field.Length, Is.EqualTo(1));
            Assert.That(field[0].Card, Is.EqualTo(lead));
            Assert.That(field[0].Position, Is.EqualTo(FieldPosition.Lead));

            DynamicBuffer<DeckCard> deck = entities.GetBuffer<DeckCard>(player);
            Assert.That(deck.Length, Is.EqualTo(2));
            Assert.That(deck[0].Card, Is.Not.EqualTo(lead));
            Assert.That(deck[1].Card, Is.Not.EqualTo(lead));

            Entity character = deck[0].Card;
            Assert.That(entities.Exists(character), Is.True);
            Assert.That(entities.HasComponent<CharacterTag>(character), Is.True);
            Assert.That(entities.HasComponent<SkillTag>(character), Is.False);
            AssertCardIdentity(character, battle, player, expectedSide, characterDefinition);

            Entity skill = deck[1].Card;
            Assert.That(entities.Exists(skill), Is.True);
            Assert.That(entities.HasComponent<SkillTag>(skill), Is.True);
            Assert.That(entities.HasComponent<CharacterTag>(skill), Is.False);
            AssertCardIdentity(skill, battle, player, expectedSide, skillDefinition);
        }

        private void AssertCardIdentity(
            Entity card,
            Entity battle,
            Entity owner,
            BattleSide side,
            uint definitionID
        )
        {
            Assert.That(entities.GetComponentData<CardOwner>(card).Player, Is.EqualTo(owner));
            Assert.That(entities.GetComponentData<OwnedBattle>(card).Battle, Is.EqualTo(battle));
            Assert.That(
                entities.GetComponentData<BattleSideAssignment>(card).Side,
                Is.EqualTo(side)
            );
            Assert.That(
                entities.GetComponentData<CardDefinitionID>(card).Value,
                Is.EqualTo(definitionID)
            );
            Assert.That(entities.HasComponent<CardRuntimeID>(card), Is.True);
        }

        private void AssertRejectedWithoutConstruction(Scenario scenario)
        {
            Assert.That(
                entities.HasComponent<RosterInitialisationFailed>(scenario.Request),
                Is.True
            );
            Assert.That(entities.HasComponent<RosterReadyRequest>(scenario.Request), Is.False);
            Assert.That(
                entities.HasComponent<InitialRosterConstructionRequest>(scenario.Request),
                Is.False
            );
            Assert.That(Count<CharacterTag>(), Is.Zero);
            Assert.That(Count<SkillTag>(), Is.Zero);
            Assert.That(GetLead(scenario.PlayerA), Is.EqualTo(Entity.Null));
            Assert.That(GetLead(scenario.PlayerB), Is.EqualTo(Entity.Null));
            Assert.That(entities.GetBuffer<DeckCard>(scenario.PlayerA).Length, Is.Zero);
            Assert.That(entities.GetBuffer<DeckCard>(scenario.PlayerB).Length, Is.Zero);
            Assert.That(entities.GetBuffer<FieldCard>(scenario.PlayerA).Length, Is.Zero);
            Assert.That(entities.GetBuffer<FieldCard>(scenario.PlayerB).Length, Is.Zero);
            Assert.That(entities.GetBuffer<BattleParticipant>(scenario.Battle).Length, Is.Zero);
            Assert.That(
                entities.GetComponentData<BattleRuntimeIDCounter>(scenario.Battle).NextID,
                Is.EqualTo(FirstRuntimeID)
            );
            Assert.That(
                entities.GetComponentData<BattleState>(scenario.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup)
            );
        }
    }
}
