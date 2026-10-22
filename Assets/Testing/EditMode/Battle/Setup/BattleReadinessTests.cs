using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
using Archeus.Core.Debugging;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Archeus.Testing.Battle.Setup
{
    /// <summary>
    /// Creates a real valid mixed roster, then verifies the readiness gate.
    /// Invalid cases corrupt the *assembled* runtime state after roster creation.
    /// The content fixture mirrors the known-passing RosterInitialisationTests.
    /// </summary>
    public class BattleReadinessTests
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
        private SystemHandle readinessSystem;
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
            world = new World("Battle Readiness Tests");
            entities = world.EntityManager;
            rosterSystem = world.CreateSystem<RosterInitialisationSystem>();
            readinessSystem = world.CreateSystem<BattleReadinessSystem>();
            CreateContentFixture();

            // Our production logger is configurable and uses Debug.LogError.
            // Each rejection test explicitly expects its corresponding error.
            Logging.EnableCategory(LogCategory.Setup);
            Logging.SetMinimumLevel(LogLevel.Info);
        }

        [TearDown]
        public void TearDown()
        {
            world?.Dispose();
            lookupTables.Dispose();
            if (registryReference.IsCreated)
                registryReference.Dispose();
        }

        // =========================================================
        // Valid assembled rosters
        // =========================================================

        [Test]
        public void ValidRoster_AuthorizesBattleStart_AndPreservesSetupData()
        {
            Scenario scenario = CreateConstructedScenario();

            RunReadiness();

            AssertReady(scenario);
            Assert.That(
                entities.GetBuffer<BattlePlayerEntry>(scenario.Request).Length,
                Is.EqualTo(2)
            );
            Assert.That(
                entities.GetBuffer<PlayerDeckEntry>(scenario.Request).Length,
                Is.EqualTo(4)
            );
            Assert.That(
                entities.GetComponentData<BattleState>(scenario.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup),
                "Readiness must not begin turnflow itself."
            );
            Assert.That(Count<CharacterTag>(), Is.EqualTo(4));
            Assert.That(Count<SkillTag>(), Is.EqualTo(2));
        }

        [Test]
        public void DeckOrderMayChange_ButSameCardMultisetStillPasses()
        {
            Scenario scenario = CreateConstructedScenario();
            DynamicBuffer<DeckCard> deck = entities.GetBuffer<DeckCard>(scenario.PlayerA);
            DeckCard first = deck[0];
            deck[0] = deck[1];
            deck[1] = first;

            RunReadiness();
            AssertReady(scenario);
        }

        [Test]
        public void RepeatedReadinessUpdates_DoNotCreateAdditionalStartRequests()
        {
            Scenario scenario = CreateConstructedScenario();
            RunReadiness();
            RunReadiness();
            RunReadiness();

            AssertReady(scenario);
            Assert.That(Count<BattleStartRequest>(), Is.EqualTo(1));
        }

        // =========================================================
        // Invalid requests / battle root
        // =========================================================

        [Test]
        public void MissingConfigurationBuffer_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.RemoveComponent<PlayerDeckEntry>(scenario.Request);
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.InvalidSetupRequest);
        }

        [Test]
        public void ConflictingEarlierStage_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.AddComponentData(
                scenario.Request,
                new InitialRosterConstructionRequest { Battle = scenario.Battle }
            );
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.InvalidSetupRequest);
        }

        [Test]
        public void ConflictingBattleStartRequest_IsBlockedAndRemoved()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.AddComponentData(
                scenario.Request,
                new BattleStartRequest { Battle = scenario.Battle }
            );
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.InvalidSetupRequest);
        }

        [Test]
        public void AlreadyFailedSetupEntity_CannotBeReauthorized()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.AddComponentData(
                scenario.Request,
                new BattleReadinessFailed
                {
                    Battle = scenario.Battle,
                    Reason = BattleReadinessFailureReason.DeckMismatch,
                }
            );
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.InvalidSetupRequest);
        }

        [Test]
        public void DuplicateReadyRequestsForSameBattle_RejectBoth()
        {
            Scenario scenario = CreateConstructedScenario();
            Entity duplicate = entities.CreateEntity();
            entities.AddComponentData(
                duplicate,
                new RosterReadyRequest { Battle = scenario.Battle }
            );

            ExpectError(BattleReadinessFailureReason.DuplicateReadinessRequest);
            ExpectError(BattleReadinessFailureReason.DuplicateReadinessRequest);
            RunReadiness();

            AssertFailure(
                scenario.Request,
                scenario.Battle,
                BattleReadinessFailureReason.DuplicateReadinessRequest
            );
            AssertFailure(
                duplicate,
                scenario.Battle,
                BattleReadinessFailureReason.DuplicateReadinessRequest
            );
            Assert.That(Count<BattleStartRequest>(), Is.Zero);
        }

        [Test]
        public void WrongBattlePhase_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.SetComponentData(
                scenario.Battle,
                new BattleState { Phase = BattlePhase.TurnStart }
            );
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.InvalidBattle);
        }

        [Test]
        public void MissingBattleParticipantBuffer_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.RemoveComponent<BattleParticipant>(scenario.Battle);
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.InvalidBattle);
        }

        [Test]
        public void InvalidBattleRegistryReference_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.SetComponentData(scenario.Battle, new BattleContentRegistry());
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.InvalidBattle);
        }

        // =========================================================
        // Invalid player registration
        // =========================================================

        [Test]
        public void MissingPlayerReference_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.GetBuffer<BattlePlayerReference>(scenario.Battle).RemoveAt(1);
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.PlayerMismatch);
        }

        [Test]
        public void DuplicatePlayerReference_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            DynamicBuffer<BattlePlayerReference> players =
                entities.GetBuffer<BattlePlayerReference>(scenario.Battle);
            players[1] = players[0];
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.PlayerMismatch);
        }

        [Test]
        public void PlayersAssignedToSameSide_AreRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.SetComponentData(
                scenario.PlayerB,
                new BattleSideAssignment { Side = BattleSide.SideA }
            );
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.PlayerMismatch);
        }

        [Test]
        public void PlayerReferencesWrongBattle_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.SetComponentData(scenario.PlayerB, new OwnedBattle { Battle = Entity.Null });
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.PlayerMismatch);
        }

        // =========================================================
        // Corrupted Leads
        // =========================================================

        [Test]
        public void MissingLeadReference_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.SetComponentData(scenario.PlayerA, new LeadCharacter { Value = Entity.Null });
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.LeadMismatch);
        }

        [Test]
        public void LeadPlacedInWrongFieldPosition_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            DynamicBuffer<FieldCard> field = entities.GetBuffer<FieldCard>(scenario.PlayerA);
            FieldCard entry = field[0];
            entry.Position = FieldPosition.AttackingForceSlot1;
            field[0] = entry;
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.LeadMismatch);
        }

        [Test]
        public void LeadOwnedByAnotherPlayer_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.SetComponentData(
                GetLead(scenario.PlayerA),
                new CardOwner { Player = scenario.PlayerB }
            );
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.LeadMismatch);
        }

        [Test]
        public void NonFiniteLeadHealth_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.SetComponentData(
                GetLead(scenario.PlayerA),
                new CurrentHealth { Value = float.NaN }
            );
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.LeadMismatch);
        }

        // =========================================================
        // Corrupted deck cards / runtime IDs
        // =========================================================

        [Test]
        public void MissingDeckEntry_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.GetBuffer<DeckCard>(scenario.PlayerA).RemoveAt(0);
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.DeckMismatch);
        }

        [Test]
        public void SkillOwnershipCorruption_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            Entity skill = GetDeckCard(scenario.PlayerA, 1);
            entities.SetComponentData(skill, new CardOwner { Player = scenario.PlayerB });
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.DeckMismatch);
        }

        [Test]
        public void WrongCardType_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            Entity skill = GetDeckCard(scenario.PlayerA, 1);
            entities.RemoveComponent<SkillTag>(skill);
            entities.AddComponent<CharacterTag>(skill);
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.DeckMismatch);
        }

        [Test]
        public void TwoCardsUsingSameRuntimeID_AreRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            uint leadID = entities.GetComponentData<CardRuntimeID>(GetLead(scenario.PlayerA)).Value;
            entities.SetComponentData(
                GetDeckCard(scenario.PlayerA, 0),
                new CardRuntimeID { Value = leadID }
            );
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.DeckMismatch);
        }

        [Test]
        public void SkillWithNonFiniteStats_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            Entity skill = GetDeckCard(scenario.PlayerA, 1);
            SkillRuntimeStatBaseline stats = entities.GetComponentData<SkillRuntimeStatBaseline>(
                skill
            );
            stats.Attack = float.PositiveInfinity;
            entities.SetComponentData(skill, stats);
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.DeckMismatch);
        }

        [Test]
        public void DuplicatedDeckEntityReference_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            DynamicBuffer<DeckCard> deck = entities.GetBuffer<DeckCard>(scenario.PlayerA);
            deck[1] = deck[0];
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.DeckMismatch);
        }

        // =========================================================
        // Participant and orphan protection
        // =========================================================

        [Test]
        public void MissingLeadParticipant_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            entities.GetBuffer<BattleParticipant>(scenario.Battle).RemoveAt(0);
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.ParticipantMismatch);
        }

        [Test]
        public void DuplicateLeadParticipant_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            DynamicBuffer<BattleParticipant> participants = entities.GetBuffer<BattleParticipant>(
                scenario.Battle
            );
            participants[1] = participants[0];
            AssertRejectedAfterRun(scenario, BattleReadinessFailureReason.ParticipantMismatch);
        }

        [Test]
        public void OrphanedCharacter_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            Entity orphan = entities.CreateEntity();
            entities.AddComponent<CharacterTag>(orphan);
            entities.AddComponentData(orphan, new OwnedBattle { Battle = scenario.Battle });
            AssertRejectedAfterRun(
                scenario,
                BattleReadinessFailureReason.UnregisteredRuntimeEntity
            );
        }

        [Test]
        public void OrphanedRuntimePlayer_IsRejected()
        {
            Scenario scenario = CreateConstructedScenario();
            Entity orphan = entities.CreateEntity();
            entities.AddComponent<PlayerTag>(orphan);
            entities.AddComponentData(orphan, new OwnedBattle { Battle = scenario.Battle });
            AssertRejectedAfterRun(
                scenario,
                BattleReadinessFailureReason.UnregisteredRuntimeEntity
            );
        }

        [Test]
        public void OneInvalidBattle_DoesNotPreventOtherBattleFromStarting()
        {
            Scenario good = CreateScenario(42, 1001, 1002);
            Scenario bad = CreateScenario(43, 2001, 2002);
            RunRoster();
            Assert.That(entities.HasComponent<RosterReadyRequest>(good.Request), Is.True);
            Assert.That(entities.HasComponent<RosterReadyRequest>(bad.Request), Is.True);

            entities.GetBuffer<BattleParticipant>(bad.Battle).RemoveAt(0);
            ExpectError(BattleReadinessFailureReason.ParticipantMismatch);
            RunReadiness();

            AssertReady(good);
            AssertFailure(
                bad.Request,
                bad.Battle,
                BattleReadinessFailureReason.ParticipantMismatch
            );
        }

        // =========================================================
        // Gate helpers
        // =========================================================

        private Scenario CreateConstructedScenario()
        {
            Scenario scenario = CreateScenario();
            RunRoster();
            Assert.That(
                entities.HasComponent<RosterReadyRequest>(scenario.Request),
                Is.True,
                "RosterInitialisationSystem must issue RosterReadyRequest."
            );
            return scenario;
        }

        private void RunReadiness() => readinessSystem.Update(world.Unmanaged);

        private void ExpectError(BattleReadinessFailureReason reason)
        {
            // Logging.Error maps to Debug.LogError in the editor.
            // Intentionally rejected scenarios must register an expected log.
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\[BattleReadiness\] FAILED.*Reason=" + reason + @"\b")
            );
        }

        private void AssertRejectedAfterRun(
            Scenario scenario,
            BattleReadinessFailureReason expectedReason
        )
        {
            ExpectError(expectedReason);
            RunReadiness();
            AssertFailure(scenario.Request, scenario.Battle, expectedReason);
            Assert.That(Count<BattleStartRequest>(), Is.Zero);
        }

        private void AssertFailure(
            Entity request,
            Entity battle,
            BattleReadinessFailureReason expectedReason
        )
        {
            Assert.That(entities.Exists(request), Is.True);
            Assert.That(entities.HasComponent<RosterReadyRequest>(request), Is.False);
            Assert.That(entities.HasComponent<BattleStartRequest>(request), Is.False);
            Assert.That(entities.HasComponent<BattleReadinessFailed>(request), Is.True);
            BattleReadinessFailed failure = entities.GetComponentData<BattleReadinessFailed>(
                request
            );
            Assert.That(failure.Battle, Is.EqualTo(battle));
            Assert.That(failure.Reason, Is.EqualTo(expectedReason));
        }

        private void AssertReady(Scenario scenario)
        {
            Assert.That(entities.HasComponent<RosterReadyRequest>(scenario.Request), Is.False);
            Assert.That(entities.HasComponent<BattleReadinessFailed>(scenario.Request), Is.False);
            Assert.That(entities.HasComponent<BattleStartRequest>(scenario.Request), Is.True);
            Assert.That(
                entities.GetComponentData<BattleStartRequest>(scenario.Request).Battle,
                Is.EqualTo(scenario.Battle)
            );
        }

        // =========================================================
        // Scenario setup / content fixture (same as roster tests)
        // =========================================================

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
    }
}
