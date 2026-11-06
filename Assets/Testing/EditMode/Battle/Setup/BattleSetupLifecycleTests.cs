using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Systems.Setup;
using Archeus.Content.Lookup;
using Archeus.Content.Registries;
using Archeus.Game.Bootstrap;
using NUnit.Framework;
using Unity.Entities;

namespace Archeus.Testing.Battle.Setup
{
    public class BattleSetupLifecycleTests
    {
        private const ulong TestSeed = 12345678UL;

        private World testWorld;
        private EntityManager entities;

        private SystemHandle creationSystem;
        private SystemHandle rosterSystem;
        private SystemHandle phaseTransitionSystem;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("Battle Setup Lifecycle Tests");
            entities = testWorld.EntityManager;

            creationSystem = testWorld.CreateSystem<BattleCreationSystem>();

            rosterSystem = testWorld.CreateSystem<RosterInitialisationSystem>();

            phaseTransitionSystem = testWorld.CreateSystem<BattlePhaseTransitionSystem>();

            // Battle creation copies the registry reference
            // without dereferencing it.
            entities.CreateEntity(
                typeof(BattleContentReadyTag),
                typeof(ContentBlobRegistryComponent)
            );
        }

        [TearDown]
        public void TearDown()
        {
            testWorld?.Dispose();
        }

        // ======================================================
        // CREATION -> ROSTER HANDOFF
        // ======================================================

        [Test]
        public void BattleCreation_AdvancesToRosterRequest()
        {
            Entity request = CreateValidRequest();

            RunCreation();

            Assert.That(entities.Exists(request), Is.True);

            Assert.That(entities.HasComponent<StartBattleRequest>(request), Is.False);

            Assert.That(entities.HasComponent<InitialRosterConstructionRequest>(request), Is.True);

            Assert.That(entities.HasComponent<RosterReadyRequest>(request), Is.False);

            Assert.That(entities.HasComponent<RosterInitialisationFailed>(request), Is.False);

            Entity battle = GetBattleFromRosterRequest(request);

            Assert.That(entities.Exists(battle), Is.True);
            AssertBattlePhase(battle, BattlePhase.Setup);
        }

        [Test]
        public void BattleCreation_PreservesOriginalRosterBuffers()
        {
            Entity request = CreateValidRequest();

            RunCreation();

            DynamicBuffer<BattlePlayerEntry> players = entities.GetBuffer<BattlePlayerEntry>(
                request
            );

            DynamicBuffer<PlayerDeckEntry> cards = entities.GetBuffer<PlayerDeckEntry>(request);

            Assert.That(players.Length, Is.EqualTo(2));
            Assert.That(cards.Length, Is.EqualTo(2));

            Assert.That(players[0].PlayerID, Is.EqualTo(1001UL));
            Assert.That(players[0].LeadCharacterDefinitionID, Is.EqualTo(1U));

            Assert.That(players[1].PlayerID, Is.EqualTo(1002UL));
            Assert.That(players[1].LeadCharacterDefinitionID, Is.EqualTo(2U));

            Assert.That(cards[0].PlayerID, Is.EqualTo(1001UL));
            Assert.That(cards[0].CardDefinitionID, Is.EqualTo(3U));
            Assert.That(cards[0].CardType, Is.EqualTo(RuntimeCardType.Character));

            Assert.That(cards[1].PlayerID, Is.EqualTo(1002UL));
            Assert.That(cards[1].CardDefinitionID, Is.EqualTo(4U));
            Assert.That(cards[1].CardType, Is.EqualTo(RuntimeCardType.Skill));
        }

        [Test]
        public void RepeatedCreationUpdates_DoNotDuplicateBattle()
        {
            Entity request = CreateValidRequest();

            RunCreation();
            RunCreation();
            RunCreation();

            Assert.That(CountBattles(), Is.EqualTo(1));
            Assert.That(CountPlayers(), Is.EqualTo(2));

            Assert.That(entities.HasComponent<StartBattleRequest>(request), Is.False);

            Assert.That(entities.HasComponent<InitialRosterConstructionRequest>(request), Is.True);
        }

        [Test]
        public void MultipleBattles_HaveIndependentRosterRequests()
        {
            Entity request1 = CreateValidRequest(
                battleID: 42,
                firstPlayerID: 1001,
                secondPlayerID: 1002
            );

            Entity request2 = CreateValidRequest(
                battleID: 43,
                firstPlayerID: 2001,
                secondPlayerID: 2002
            );

            RunCreation();

            Entity battle1 = GetBattleFromRosterRequest(request1);
            Entity battle2 = GetBattleFromRosterRequest(request2);

            Assert.That(battle1, Is.Not.EqualTo(battle2));

            Assert.That(CountBattles(), Is.EqualTo(2));
            Assert.That(CountPlayers(), Is.EqualTo(4));

            AssertBattlePhase(battle1, BattlePhase.Setup);
            AssertBattlePhase(battle2, BattlePhase.Setup);

            Assert.That(entities.GetComponentData<BattleID>(battle1).Value, Is.EqualTo(42UL));

            Assert.That(entities.GetComponentData<BattleID>(battle2).Value, Is.EqualTo(43UL));

            Assert.That(entities.HasComponent<InitialRosterConstructionRequest>(request1), Is.True);

            Assert.That(entities.HasComponent<InitialRosterConstructionRequest>(request2), Is.True);
        }

        // ======================================================
        // SETUP PHASE BEHAVIOUR
        // ======================================================

        [Test]
        public void SetupPhase_DoesNotAdvanceAutomatically()
        {
            Entity request = CreateValidRequest();

            RunCreation();

            Entity battle = GetBattleFromRosterRequest(request);

            RunPhaseTransition();
            RunPhaseTransition();
            RunPhaseTransition();

            AssertBattlePhase(battle, BattlePhase.Setup);

            Assert.That(entities.HasComponent<InitialRosterConstructionRequest>(request), Is.True);
        }

        [Test]
        public void RosterReadyRequest_DoesNotStartGameplay()
        {
            Entity request = CreateValidRequest();

            RunCreation();

            Entity battle = GetBattleFromRosterRequest(request);

            // Simulate the stage handoff without constructing a
            // roster. This deliberately tests phase isolation,
            // not successful roster construction.
            entities.RemoveComponent<InitialRosterConstructionRequest>(request);

            entities.AddComponentData(request, new RosterReadyRequest { Battle = battle });

            RunPhaseTransition();

            AssertBattlePhase(battle, BattlePhase.Setup);
        }

        // ======================================================
        // ROSTER SYSTEM DEPENDENCIES AND FAILURE HANDLING
        // ======================================================

        [Test]
        public void RosterInitialisation_WaitsForContentLookup()
        {
            Entity request = CreateValidRequest();

            RunCreation();

            Entity battle = GetBattleFromRosterRequest(request);

            // ContentLookupTables doesn't exist in this world.
            // RequireForUpdate should prevent execution.
            RunRoster();

            Assert.That(entities.HasComponent<InitialRosterConstructionRequest>(request), Is.True);

            Assert.That(entities.HasComponent<RosterReadyRequest>(request), Is.False);

            AssertBattlePhase(battle, BattlePhase.Setup);
            AssertNoRuntimeCards();
        }

        [Test]
        public void InvalidContentRegistry_RejectsRosterWithoutCards()
        {
            Entity request = CreateValidRequest();

            RunCreation();

            Entity battle = GetBattleFromRosterRequest(request);

            // This supplies the required singleton, but not a
            // valid ContentBlobRegistry blob reference.
            entities.CreateEntity(typeof(ContentLookupTables));

            RunRoster();

            Assert.That(entities.HasComponent<InitialRosterConstructionRequest>(request), Is.False);

            Assert.That(entities.HasComponent<RosterInitialisationFailed>(request), Is.True);

            Assert.That(entities.HasComponent<RosterReadyRequest>(request), Is.False);

            AssertNoRuntimeCards();

            // Rejected initialization must not consume IDs.
            Assert.That(
                entities.GetComponentData<BattleRuntimeIDCounter>(battle).NextID,
                Is.EqualTo(100U)
            );

            // The input data remains available for diagnostics.
            Assert.That(entities.GetBuffer<BattlePlayerEntry>(request).Length, Is.EqualTo(2));

            Assert.That(entities.GetBuffer<PlayerDeckEntry>(request).Length, Is.EqualTo(2));

            AssertBattlePhase(battle, BattlePhase.Setup);
        }

        [Test]
        public void RejectedRoster_IsNotProcessedAgain()
        {
            Entity request = CreateValidRequest();

            RunCreation();

            entities.CreateEntity(typeof(ContentLookupTables));

            RunRoster();
            RunRoster();
            RunRoster();

            Assert.That(entities.HasComponent<RosterInitialisationFailed>(request), Is.True);

            Assert.That(entities.HasComponent<InitialRosterConstructionRequest>(request), Is.False);

            AssertNoRuntimeCards();
        }

        // ======================================================
        // GAMEPLAY PHASE REGRESSION
        // ======================================================

        [Test]
        public void TurnStartCompletion_StillAdvancesToDrawing()
        {
            using var fixture = new Archeus.Testing.Battle.Snapshots.SnapshotTestFixture(testWorld);
            Entity battle = fixture.Battle;
            Archeus.Battle.Runtime.AttackingStageExecutionManager.SetPhase(
                entities,
                battle,
                BattlePhase.TurnStart
            );
            entities.AddComponent<BattleTurnStartCompleteTag>(battle);

            RunPhaseTransition();

            AssertBattlePhase(battle, BattlePhase.Drawing);

            Assert.That(entities.HasComponent<BattleTurnStartCompleteTag>(battle), Is.False);
        }

        // ======================================================
        // TEST HELPERS
        // ======================================================

        private Entity CreateValidRequest(
            ulong battleID = 42,
            ulong firstPlayerID = 1001,
            ulong secondPlayerID = 1002
        )
        {
            Entity request = entities.CreateEntity();

            entities.AddComponentData(
                request,
                new StartBattleRequest
                {
                    BattleID = battleID,
                    BattleSeed = TestSeed,
                    BattleConfigID = 0,
                }
            );

            DynamicBuffer<BattlePlayerEntry> players = entities.AddBuffer<BattlePlayerEntry>(
                request
            );

            players.Add(
                new BattlePlayerEntry { PlayerID = firstPlayerID, LeadCharacterDefinitionID = 1 }
            );

            players.Add(
                new BattlePlayerEntry { PlayerID = secondPlayerID, LeadCharacterDefinitionID = 2 }
            );

            DynamicBuffer<PlayerDeckEntry> cards = entities.AddBuffer<PlayerDeckEntry>(request);

            cards.Add(
                new PlayerDeckEntry
                {
                    PlayerID = firstPlayerID,
                    CardDefinitionID = 3,
                    CardType = RuntimeCardType.Character,
                }
            );

            cards.Add(
                new PlayerDeckEntry
                {
                    PlayerID = secondPlayerID,
                    CardDefinitionID = 4,
                    CardType = RuntimeCardType.Skill,
                }
            );

            return request;
        }

        private Entity GetBattleFromRosterRequest(Entity request)
        {
            Assert.That(
                entities.HasComponent<InitialRosterConstructionRequest>(request),
                Is.True,
                "Expected an InitialRosterConstructionRequest."
            );

            return entities.GetComponentData<InitialRosterConstructionRequest>(request).Battle;
        }

        private void RunCreation()
        {
            creationSystem.Update(testWorld.Unmanaged);
        }

        private void RunRoster()
        {
            rosterSystem.Update(testWorld.Unmanaged);
        }

        private void RunPhaseTransition()
        {
            phaseTransitionSystem.Update(testWorld.Unmanaged);
        }

        private void AssertBattlePhase(Entity battle, BattlePhase expected)
        {
            Assert.That(entities.Exists(battle), Is.True);

            Assert.That(entities.GetComponentData<BattleState>(battle).Phase, Is.EqualTo(expected));
        }

        private void AssertNoRuntimeCards()
        {
            using var characters = entities.CreateEntityQuery(
                ComponentType.ReadOnly<CharacterTag>()
            );

            using var skills = entities.CreateEntityQuery(ComponentType.ReadOnly<SkillTag>());

            Assert.That(characters.CalculateEntityCount(), Is.Zero);

            Assert.That(skills.CalculateEntityCount(), Is.Zero);
        }

        private int CountBattles()
        {
            using var query = entities.CreateEntityQuery(ComponentType.ReadOnly<BattleTag>());

            return query.CalculateEntityCount();
        }

        private int CountPlayers()
        {
            using var query = entities.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());

            return query.CalculateEntityCount();
        }
    }
}
