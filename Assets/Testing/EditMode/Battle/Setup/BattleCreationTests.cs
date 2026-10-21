using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Systems.Setup;
using Archeus.Content.Registries;
using Archeus.Game.Bootstrap;
using NUnit.Framework;
using Unity.Entities;

namespace Archeus.Testing.Battle.Setup
{
    public class BattleCreationTests
    {
        private const ulong TestSeed = 12345678UL;

        private World testWorld;
        private EntityManager entities;
        private SystemHandle creationSystem;
        private Entity contentEntity;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("Battle Creation Tests");
            entities = testWorld.EntityManager;

            creationSystem = testWorld.CreateSystem<BattleCreationSystem>();

            // The creation system requires content to be ready.
            // This test fixture does not construct runtime cards,
            // so a default registry reference is sufficient for now.
            contentEntity = entities.CreateEntity(
                typeof(BattleContentReadyTag),
                typeof(ContentBlobRegistryComponent)
            );
        }

        [TearDown]
        public void TearDown()
        {
            testWorld?.Dispose();
        }

        // ==========================================================
        // VALID BATTLE CREATION
        // ==========================================================

        [Test]
        public void ValidRequest_CreatesBattleAndPlayers()
        {
            Entity request = CreateValidRequest();

            RunCreation();

            Assert.That(CountBattles(), Is.EqualTo(1));
            Assert.That(CountPlayers(), Is.EqualTo(2));

            Entity battle = GetBattleForRequest(request);

            Assert.That(entities.GetComponentData<BattleID>(battle).Value, Is.EqualTo(42UL));

            DynamicBuffer<BattlePlayerReference> references =
                entities.GetBuffer<BattlePlayerReference>(battle);

            Assert.That(references.Length, Is.EqualTo(2));

            Entity player1 = GetPlayerByID(battle, 1001);
            Entity player2 = GetPlayerByID(battle, 1002);

            Assert.That(player1, Is.Not.EqualTo(player2));
            Assert.That(entities.Exists(player1), Is.True);
            Assert.That(entities.Exists(player2), Is.True);

            Assert.That(entities.GetComponentData<OwnedBattle>(player1).Battle, Is.EqualTo(battle));

            Assert.That(entities.GetComponentData<OwnedBattle>(player2).Battle, Is.EqualTo(battle));

            BattleSide side1 = entities.GetComponentData<Team>(player1).Side;

            BattleSide side2 = entities.GetComponentData<Team>(player2).Side;

            Assert.That(
                new[] { side1, side2 },
                Is.EquivalentTo(new[] { BattleSide.SideA, BattleSide.SideB })
            );

            // Request must advance to the next setup stage.
            Assert.That(entities.Exists(request), Is.True);

            Assert.That(entities.HasComponent<StartBattleRequest>(request), Is.False);

            Assert.That(entities.HasComponent<InitialRosterConstructionRequest>(request), Is.True);
        }

        [Test]
        public void ValidRequest_PreservesFullConfiguration()
        {
            Entity request = CreateValidRequest();

            DynamicBuffer<PlayerDeckEntry> cards = entities.GetBuffer<PlayerDeckEntry>(request);

            // Include a Skill to verify its type is preserved.
            cards.Add(
                new PlayerDeckEntry
                {
                    PlayerID = 1001,
                    CardDefinitionID = 5,
                    CardType = RuntimeCardType.Skill,
                }
            );

            DynamicBuffer<BattlePlayerEntry> players = entities.GetBuffer<BattlePlayerEntry>(
                request
            );

            BattlePlayerEntry[] expectedPlayers = new BattlePlayerEntry[players.Length];

            PlayerDeckEntry[] expectedCards = new PlayerDeckEntry[cards.Length];

            for (int i = 0; i < players.Length; i++)
                expectedPlayers[i] = players[i];

            for (int i = 0; i < cards.Length; i++)
                expectedCards[i] = cards[i];

            RunCreation();

            Entity battle = GetBattleForRequest(request);

            Assert.That(entities.Exists(battle), Is.True);

            DynamicBuffer<BattlePlayerEntry> actualPlayers = entities.GetBuffer<BattlePlayerEntry>(
                request
            );

            DynamicBuffer<PlayerDeckEntry> actualCards = entities.GetBuffer<PlayerDeckEntry>(
                request
            );

            Assert.That(actualPlayers.Length, Is.EqualTo(expectedPlayers.Length));

            Assert.That(actualCards.Length, Is.EqualTo(expectedCards.Length));

            // Verify player identities, leads and ordering.
            for (int i = 0; i < expectedPlayers.Length; i++)
            {
                Assert.That(actualPlayers[i].PlayerID, Is.EqualTo(expectedPlayers[i].PlayerID));

                Assert.That(
                    actualPlayers[i].LeadCharacterDefinitionID,
                    Is.EqualTo(expectedPlayers[i].LeadCharacterDefinitionID)
                );
            }

            // Verify card ownership, definitions, types and order.
            for (int i = 0; i < expectedCards.Length; i++)
            {
                Assert.That(actualCards[i].PlayerID, Is.EqualTo(expectedCards[i].PlayerID));

                Assert.That(
                    actualCards[i].CardDefinitionID,
                    Is.EqualTo(expectedCards[i].CardDefinitionID)
                );

                Assert.That(actualCards[i].CardType, Is.EqualTo(expectedCards[i].CardType));
            }
        }

        // ==========================================================
        // INPUT VALIDATION
        // ==========================================================

        [Test]
        public void DuplicatePlayerIDs_DoesNotCreateBattle()
        {
            Entity request = CreateValidRequest();

            DynamicBuffer<BattlePlayerEntry> players = entities.GetBuffer<BattlePlayerEntry>(
                request
            );

            BattlePlayerEntry secondPlayer = players[1];
            secondPlayer.PlayerID = players[0].PlayerID;
            players[1] = secondPlayer;

            RunCreation();

            AssertNoConstruction(request);
        }

        [Test]
        public void UnknownCardOwner_DoesNotCreateBattle()
        {
            Entity request = CreateValidRequest();

            DynamicBuffer<PlayerDeckEntry> cards = entities.GetBuffer<PlayerDeckEntry>(request);

            PlayerDeckEntry firstCard = cards[0];
            firstCard.PlayerID = 9999;
            cards[0] = firstCard;

            RunCreation();

            AssertNoConstruction(request);
        }

        [Test]
        public void MissingRequiredBuffer_DoesNotCreateBattle()
        {
            Entity request = entities.CreateEntity();

            entities.AddComponentData(
                request,
                new StartBattleRequest
                {
                    BattleID = 42,
                    BattleSeed = TestSeed,
                    BattleConfigID = 0,
                }
            );

            // Deliberately omit PlayerDeckEntry.
            entities.AddBuffer<BattlePlayerEntry>(request);

            RunCreation();

            AssertNoConstruction(request);
        }

        // ==========================================================
        // REQUEST LIFECYCLE
        // ==========================================================

        [Test]
        public void ProcessedRequest_CannotCreateDuplicateBattle()
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

        // ==========================================================
        // MULTIPLE BATTLES
        // ==========================================================

        [Test]
        public void MultipleRequests_CreateIndependentBattles()
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

            Assert.That(CountBattles(), Is.EqualTo(2));
            Assert.That(CountPlayers(), Is.EqualTo(4));

            Entity battle1 = GetBattleForRequest(request1);
            Entity battle2 = GetBattleForRequest(request2);

            Assert.That(battle1, Is.Not.EqualTo(battle2));

            Assert.That(entities.GetComponentData<BattleID>(battle1).Value, Is.EqualTo(42UL));

            Assert.That(entities.GetComponentData<BattleID>(battle2).Value, Is.EqualTo(43UL));

            Entity player1 = GetPlayerByID(battle1, 1001);
            Entity player2 = GetPlayerByID(battle2, 2001);

            Assert.That(player1, Is.Not.EqualTo(player2));

            Assert.That(
                entities.GetComponentData<OwnedBattle>(player1).Battle,
                Is.EqualTo(battle1)
            );

            Assert.That(
                entities.GetComponentData<OwnedBattle>(player2).Battle,
                Is.EqualTo(battle2)
            );

            // Each battle must register only its own participants.
            AssertBattlePlayerIDs(battle1, 1001, 1002);
            AssertBattlePlayerIDs(battle2, 2001, 2002);
        }

        // ==========================================================
        // DETERMINISM
        // ==========================================================

        [Test]
        public void SameSeed_ProducesSameSideAssignmentRegardlessOfPlayerOrder()
        {
            // Both requests use the same seed and player identities,
            // but their player buffers are ordered differently.
            Entity request1 = CreateValidRequest(battleID: 42, reversePlayerOrder: false);

            Entity request2 = CreateValidRequest(battleID: 43, reversePlayerOrder: true);

            RunCreation();

            Entity battle1 = GetBattleForRequest(request1);
            Entity battle2 = GetBattleForRequest(request2);

            Assert.That(GetPlayerSide(battle1, 1001), Is.EqualTo(GetPlayerSide(battle2, 1001)));

            Assert.That(GetPlayerSide(battle1, 1002), Is.EqualTo(GetPlayerSide(battle2, 1002)));
        }

        // ==========================================================
        // INITIAL GAMEPLAY STATE
        // ==========================================================

        [Test]
        public void CreatedBattle_HasCorrectInitialState()
        {
            Entity request = CreateValidRequest();

            RunCreation();

            Entity battle = GetBattleForRequest(request);

            Assert.That(
                entities.GetComponentData<BattleState>(battle).Phase,
                Is.EqualTo(BattlePhase.Creating)
            );

            Assert.That(entities.GetComponentData<TurnCounter>(battle).CurrentTurn, Is.EqualTo(0));

            Assert.That(entities.HasComponent<ActiveTurnPlayer>(battle), Is.True);

            Assert.That(entities.HasComponent<BattleInitialisationCompleteTag>(battle), Is.True);

            // Side assignment consumes one RNG roll.
            DeterministicRNG generator = new DeterministicRNG(TestSeed);

            BattleRNG expectedRNG = new BattleRNG
            {
                StateA = generator.StateA,
                StateB = generator.StateB,
            };

            BattleRNGService.RollBool(ref expectedRNG);

            BattleRNG actualRNG = entities.GetComponentData<BattleRNG>(battle);

            Assert.That(actualRNG.StateA, Is.EqualTo(expectedRNG.StateA));

            Assert.That(actualRNG.StateB, Is.EqualTo(expectedRNG.StateB));

            // Verify that players have their starting resources.
            foreach (
                BattlePlayerReference reference in entities.GetBuffer<BattlePlayerReference>(battle)
            )
            {
                Entity player = reference.Player;

                Assert.That(
                    entities.GetComponentData<MaxActionPoints>(player).Value,
                    Is.EqualTo(4)
                );

                Assert.That(
                    entities.GetComponentData<RemainingActionPoints>(player).Value,
                    Is.EqualTo(4)
                );

                Assert.That(entities.GetComponentData<MaxHandSize>(player).Value, Is.EqualTo(4));

                // Roster construction hasn't happened yet.
                Assert.That(entities.GetBuffer<DeckCard>(player).Length, Is.Zero);

                Assert.That(entities.GetBuffer<HandCard>(player).Length, Is.Zero);

                Assert.That(entities.GetBuffer<FieldCard>(player).Length, Is.Zero);
            }
        }

        // ==========================================================
        // CONTENT DEPENDENCIES
        // ==========================================================

        [Test]
        public void ContentNotReady_PreventsBattleCreation()
        {
            Entity request = CreateValidRequest();

            entities.RemoveComponent<BattleContentReadyTag>(contentEntity);

            RunCreation();

            AssertNoConstruction(request);
        }

        // ==========================================================
        // SHARED TEST HELPERS
        // ==========================================================

        private Entity CreateValidRequest(
            ulong battleID = 42,
            ulong firstPlayerID = 1001,
            ulong secondPlayerID = 1002,
            bool reversePlayerOrder = false
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

            BattlePlayerEntry first = new BattlePlayerEntry
            {
                PlayerID = firstPlayerID,
                LeadCharacterDefinitionID = 1,
            };

            BattlePlayerEntry second = new BattlePlayerEntry
            {
                PlayerID = secondPlayerID,
                LeadCharacterDefinitionID = 2,
            };

            if (reversePlayerOrder)
            {
                players.Add(second);
                players.Add(first);
            }
            else
            {
                players.Add(first);
                players.Add(second);
            }

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
                    CardType = RuntimeCardType.Character,
                }
            );

            return request;
        }

        private void RunCreation()
        {
            creationSystem.Update(testWorld.Unmanaged);
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

        private Entity GetBattleForRequest(Entity request)
        {
            Assert.That(
                entities.HasComponent<InitialRosterConstructionRequest>(request),
                Is.True,
                "Request has not advanced to roster construction."
            );

            return entities.GetComponentData<InitialRosterConstructionRequest>(request).Battle;
        }

        private Entity GetPlayerByID(Entity battle, ulong playerID)
        {
            DynamicBuffer<BattlePlayerReference> references =
                entities.GetBuffer<BattlePlayerReference>(battle);

            foreach (BattlePlayerReference reference in references)
            {
                Entity player = reference.Player;

                if (entities.GetComponentData<BattlePlayerIdentity>(player).PlayerID == playerID)
                {
                    return player;
                }
            }

            Assert.Fail($"Player {playerID} was not registered in the battle.");

            return Entity.Null;
        }

        private BattleSide GetPlayerSide(Entity battle, ulong playerID)
        {
            Entity player = GetPlayerByID(battle, playerID);

            return entities.GetComponentData<Team>(player).Side;
        }

        private void AssertBattlePlayerIDs(Entity battle, ulong expectedID1, ulong expectedID2)
        {
            DynamicBuffer<BattlePlayerReference> references =
                entities.GetBuffer<BattlePlayerReference>(battle);

            Assert.That(references.Length, Is.EqualTo(2));

            ulong[] actualIDs = new ulong[2];

            for (int i = 0; i < references.Length; i++)
            {
                actualIDs[i] = entities
                    .GetComponentData<BattlePlayerIdentity>(references[i].Player)
                    .PlayerID;
            }

            Assert.That(actualIDs, Is.EquivalentTo(new[] { expectedID1, expectedID2 }));
        }

        private void AssertNoConstruction(Entity request)
        {
            Assert.That(CountBattles(), Is.Zero);
            Assert.That(CountPlayers(), Is.Zero);

            Assert.That(entities.Exists(request), Is.True);

            Assert.That(entities.HasComponent<StartBattleRequest>(request), Is.True);

            Assert.That(entities.HasComponent<InitialRosterConstructionRequest>(request), Is.False);
        }
    }
}
