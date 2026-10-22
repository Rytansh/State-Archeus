using System.Text.RegularExpressions;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Archeus.Testing.Battle.Setup
{
    /// <summary>
    /// Run the REAL creation -> roster -> readiness systems to provide the
    /// starting state. Then test BattleStartSystem's own decisions and guards.
    /// </summary>
    public class BattleStartSystemTests
    {
        private BattleSetupTestFixture f;
        private EntityManager em;

        [SetUp]
        public void SetUp()
        {
            f = new BattleSetupTestFixture();
            em = f.Entities;
        }

        [TearDown]
        public void TearDown() => f.Dispose();

        [Test]
        public void HigherTotalInitiative_SelectsCorrectPlayerAndConsumesRequest()
        {
            var s = f.ReadyToStart();
            f.SetInitiatives(s, leadA: 100, characterA: 250, leadB: 50, characterB: 150);
            BattleRNG before = em.GetComponentData<BattleRNG>(s.Battle);

            f.RunStart();

            AssertStarted(s, BattleSetupTestFixture.PlayerAID);
            Assert.That(em.GetComponentData<BattleRNG>(s.Battle).StateA, Is.EqualTo(before.StateA));
            Assert.That(em.GetComponentData<BattleRNG>(s.Battle).StateB, Is.EqualTo(before.StateB));
            AssertNoSkipTags(s);
        }

        [Test]
        public void SuccessfulStart_RequestsPhaseTransition_WhichConsumesReadyTag()
        {
            var s = f.ReadyToStart();
            f.SetInitiatives(s, 100, 250, 50, 150);
            f.RunStart();

            // Intermediate state belongs to BattleStartSystem.
            AssertStarted(s, BattleSetupTestFixture.PlayerAID);

            // Final state belongs to BattlePhaseTransitionSystem.
            f.RunPhaseTransition();
            AssertTurnStartTransitioned(s, BattleSetupTestFixture.PlayerAID);
        }

        [Test]
        public void SecondPlayerWithHigherTotal_WinsRegardlessOfSide()
        {
            var s = f.ReadyToStart();
            f.SetInitiatives(s, 150, 10, 20, 300);
            f.RunStart();
            AssertStarted(s, BattleSetupTestFixture.PlayerBID);
            AssertNoSkipTags(s);
        }

        [Test]
        public void EqualTotals_HigherLeadInitiativeBreaksTie()
        {
            var s = f.ReadyToStart();
            f.SetInitiatives(s, 120, 80, 90, 110); // both total 200
            BattleRNG before = em.GetComponentData<BattleRNG>(s.Battle);
            f.RunStart();
            AssertStarted(s, BattleSetupTestFixture.PlayerAID);
            Assert.That(em.GetComponentData<BattleRNG>(s.Battle).StateA, Is.EqualTo(before.StateA));
            Assert.That(em.GetComponentData<BattleRNG>(s.Battle).StateB, Is.EqualTo(before.StateB));
            AssertNoSkipTags(s);
        }

        [Test]
        public void EqualTotals_HigherLeadOnSecondPlayerBreaksTie()
        {
            var s = f.ReadyToStart();
            f.SetInitiatives(s, 70, 130, 125, 75); // both total 200
            f.RunStart();
            AssertStarted(s, BattleSetupTestFixture.PlayerBID);
            AssertNoSkipTags(s);
        }

        [Test]
        public void FullTie_UsesSeededRngAndAddsTwoStageSkipsToBothPlayers()
        {
            var s = f.ReadyToStart();
            f.SetInitiatives(s, 100, 200, 100, 200);
            BattleRNG expectedState = em.GetComponentData<BattleRNG>(s.Battle);
            int expectedIndex = BattleRNGService.RollInt(ref expectedState, 0, 2);
            ulong expectedID =
                expectedIndex == 0
                    ? BattleSetupTestFixture.PlayerAID
                    : BattleSetupTestFixture.PlayerBID;

            f.RunStart();

            AssertStarted(s, expectedID);
            Assert.That(
                em.GetComponentData<BattleRNG>(s.Battle).StateA,
                Is.EqualTo(expectedState.StateA)
            );
            Assert.That(
                em.GetComponentData<BattleRNG>(s.Battle).StateB,
                Is.EqualTo(expectedState.StateB)
            );
            foreach (Entity player in new[] { s.PlayerA, s.PlayerB })
            {
                Assert.That(em.HasComponent<SkipPlanningStageTag>(player), Is.True);
                Assert.That(em.HasComponent<SkipAttackingStageTag>(player), Is.True);
                Assert.That(em.HasComponent<SkipDrawingStageTag>(player), Is.False);
            }
        }

        [Test]
        public void ReversedPlayerEntries_KeepDeterministicFullTieResult()
        {
            // Different ECS entity order, identical battle seed + logical IDs.
            var a = f.ReadyToStart(42, 987654321, reversePlayerEntries: false);
            var b = f.ReadyToStart(43, 987654321, reversePlayerEntries: true);
            f.SetInitiatives(a, 100, 200, 100, 200);
            f.SetInitiatives(b, 100, 200, 100, 200);

            f.RunStart();

            Assert.That(f.ActivePlayerID(a), Is.EqualTo(f.ActivePlayerID(b)));
            BattleRNG ar = em.GetComponentData<BattleRNG>(a.Battle);
            BattleRNG br = em.GetComponentData<BattleRNG>(b.Battle);
            Assert.That(ar.StateA, Is.EqualTo(br.StateA));
            Assert.That(ar.StateB, Is.EqualTo(br.StateB));
        }

        [Test]
        public void SkillCards_DoNotContributeToInitiative()
        {
            var s = f.ReadyToStart();
            f.SetInitiatives(s, 10, 10, 30, 30);
            // The fixture's Skill A has far larger Attack/Defense/Health
            // than the Characters. It must still make no Initiative contribution.
            Assert.That(em.HasComponent<SkillRuntimeStatBaseline>(f.DeckSkill(s.PlayerA)), Is.True);
            f.RunStart();
            AssertStarted(s, BattleSetupTestFixture.PlayerBID);
        }

        [Test]
        public void StartingTwice_DoesNotReinitializeTurnOrRng()
        {
            var s = f.ReadyToStart();
            f.RunStart();
            AssertStarted(s, BattleSetupTestFixture.PlayerAID);
            Entity player = em.GetComponentData<ActiveTurnPlayer>(s.Battle).Player;
            BattleRNG rng = em.GetComponentData<BattleRNG>(s.Battle);

            // Calling BattleStartSystem again does not consume the ready signal or reassign ownership.
            f.RunStart();
            f.RunStart();
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup)
            );
            Assert.That(em.HasComponent<BattleReadyTag>(s.Battle), Is.True);
            Assert.That(em.GetComponentData<ActiveTurnPlayer>(s.Battle).Player, Is.EqualTo(player));
            Assert.That(em.GetComponentData<TurnCounter>(s.Battle).CurrentTurn, Is.Zero);
            Assert.That(em.GetComponentData<BattleRNG>(s.Battle).StateA, Is.EqualTo(rng.StateA));
            Assert.That(f.Count<ActiveTurnPlayer>(), Is.EqualTo(1));

            f.RunPhaseTransition();
            AssertTurnStartTransitioned(s, BattleSetupTestFixture.PlayerAID);
            f.RunStart();
            f.RunPhaseTransition();
            AssertTurnStartTransitioned(s, BattleSetupTestFixture.PlayerAID);
        }

        [Test]
        public void MissingTurnCounter_IsCreatedAtZero()
        {
            var s = f.ReadyToStart();
            em.RemoveComponent<TurnCounter>(s.Battle);
            f.RunStart();
            AssertStarted(s, BattleSetupTestFixture.PlayerAID);
        }

        [Test]
        public void DuplicateStartRequests_BothRejectedAndBattleRemainsInSetup()
        {
            var s = f.ReadyToStart();
            Entity duplicate = em.CreateEntity();
            em.AddComponentData(duplicate, new BattleStartRequest { Battle = s.Battle });
            ExpectFailure(BattleStartFailureReason.DuplicateStartRequest);
            ExpectFailure(BattleStartFailureReason.DuplicateStartRequest);
            f.RunStart();
            AssertFailure(s.Request, s.Battle, BattleStartFailureReason.DuplicateStartRequest);
            AssertFailure(duplicate, s.Battle, BattleStartFailureReason.DuplicateStartRequest);
            Assert.That(f.Count<BattleStartRequest>(), Is.Zero);
        }

        [Test]
        public void ConflictingRosterRequest_RejectsStart()
        {
            var s = f.ReadyToStart();
            em.AddComponentData(
                s.Request,
                new InitialRosterConstructionRequest { Battle = s.Battle }
            );
            AssertRejected(s, BattleStartFailureReason.InvalidSetupRequest);
        }

        [Test]
        public void MissingBattleRng_RejectsStart()
        {
            var s = f.ReadyToStart();
            em.RemoveComponent<BattleRNG>(s.Battle);
            AssertRejected(s, BattleStartFailureReason.InvalidBattle);
        }

        [Test]
        public void WrongBattlePhase_RejectsStart()
        {
            var s = f.ReadyToStart();
            em.SetComponentData(s.Battle, new BattleState { Phase = BattlePhase.TurnStart });
            ExpectFailure(BattleStartFailureReason.InvalidBattle);
            f.RunStart();
            AssertFailure(
                s.Request,
                s.Battle,
                BattleStartFailureReason.InvalidBattle,
                expectedPhase: BattlePhase.TurnStart
            );
        }

        [Test]
        public void PreexistingActiveTurnPlayer_RejectsStart()
        {
            var s = f.ReadyToStart();
            em.AddComponentData(s.Battle, new ActiveTurnPlayer { Player = s.PlayerA });
            ExpectFailure(BattleStartFailureReason.InvalidBattle);
            f.RunStart();
            Assert.That(
                em.GetComponentData<BattleStartFailed>(s.Request).Reason,
                Is.EqualTo(BattleStartFailureReason.InvalidBattle)
            );
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup)
            );
            Assert.That(
                em.GetComponentData<ActiveTurnPlayer>(s.Battle).Player,
                Is.EqualTo(s.PlayerA),
                "Rejection must not overwrite earlier state."
            );
            Assert.That(em.HasComponent<BattleReadyTag>(s.Battle), Is.False);
        }

        [Test]
        public void DuplicatePlayerSide_RejectsStart()
        {
            var s = f.ReadyToStart();
            var aSide = em.GetComponentData<BattleSideAssignment>(s.PlayerA);
            em.SetComponentData(s.PlayerB, aSide);
            AssertRejected(s, BattleStartFailureReason.InvalidPlayers);
        }

        [Test]
        public void MissingLead_RejectsInitiativeCalculation()
        {
            var s = f.ReadyToStart();
            em.SetComponentData(s.PlayerA, new LeadCharacter { Value = Entity.Null });
            AssertRejected(s, BattleStartFailureReason.InvalidInitiative);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidCharacterInitiative_IsRejected(float invalid)
        {
            var s = f.ReadyToStart();
            f.SetInitiative(f.DeckCharacter(s.PlayerA), invalid);
            AssertRejected(s, BattleStartFailureReason.InvalidInitiative);
        }

        [Test]
        public void MissingRuntimeBaseline_RejectsInitiativeCalculation()
        {
            var s = f.ReadyToStart();
            em.RemoveComponent<CharacterRuntimeStatBaseline>(f.Lead(s.PlayerB));
            AssertRejected(s, BattleStartFailureReason.InvalidInitiative);
        }

        [Test]
        public void ForeignOwnedDeckCharacter_RejectsInitiativeCalculation()
        {
            var s = f.ReadyToStart();
            em.SetComponentData(f.DeckCharacter(s.PlayerA), new CardOwner { Player = s.PlayerB });
            AssertRejected(s, BattleStartFailureReason.InvalidInitiative);
        }

        [Test]
        public void OneRejectedBattle_DoesNotBlockAnother()
        {
            var good = f.ReadyToStart(42);
            var bad = f.ReadyToStart(43);
            f.SetInitiative(f.Lead(bad.PlayerA), float.NaN);
            ExpectFailure(BattleStartFailureReason.InvalidInitiative);
            f.RunStart();
            AssertStarted(good, BattleSetupTestFixture.PlayerAID);
            AssertFailure(bad.Request, bad.Battle, BattleStartFailureReason.InvalidInitiative);
        }

        private void AssertNoSkipTags(BattleSetupTestFixture.Scenario s)
        {
            foreach (Entity player in new[] { s.PlayerA, s.PlayerB })
            {
                Assert.That(em.HasComponent<SkipPlanningStageTag>(player), Is.False);
                Assert.That(em.HasComponent<SkipAttackingStageTag>(player), Is.False);
            }
        }

        // BattleStartSystem now signals readiness; it does NOT change BattleState.Phase.
        // The phase-transition system owns Setup -> TurnStart.
        private void AssertStarted(BattleSetupTestFixture.Scenario s, ulong expectedID)
        {
            Assert.That(em.Exists(s.Request), Is.False, "Successful start consumes setup entity.");
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup),
                "BattleStartSystem must leave phase transitions to BattlePhaseTransitionSystem."
            );
            Assert.That(
                em.HasComponent<BattleReadyTag>(s.Battle),
                Is.True,
                "A successful start must request the Setup -> TurnStart transition."
            );
            Assert.That(em.HasComponent<ActiveTurnPlayer>(s.Battle), Is.True);
            Assert.That(f.ActivePlayerID(s), Is.EqualTo(expectedID));
            Assert.That(em.HasComponent<TurnCounter>(s.Battle), Is.True);
            Assert.That(em.GetComponentData<TurnCounter>(s.Battle).CurrentTurn, Is.Zero);
        }

        private void AssertTurnStartTransitioned(
            BattleSetupTestFixture.Scenario s,
            ulong expectedID
        )
        {
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.TurnStart)
            );
            Assert.That(
                em.HasComponent<BattleReadyTag>(s.Battle),
                Is.False,
                "Transition system must consume the BattleReadyTag signal."
            );
            Assert.That(em.HasComponent<ActiveTurnPlayer>(s.Battle), Is.True);
            Assert.That(f.ActivePlayerID(s), Is.EqualTo(expectedID));
            Assert.That(
                em.GetComponentData<TurnCounter>(s.Battle).CurrentTurn,
                Is.Zero,
                "The first counted turn begins only when TurnStartSystem runs."
            );
        }

        private void AssertRejected(
            BattleSetupTestFixture.Scenario s,
            BattleStartFailureReason reason
        )
        {
            bool hasRng = em.HasComponent<BattleRNG>(s.Battle);
            BattleRNG before = hasRng ? em.GetComponentData<BattleRNG>(s.Battle) : default;
            ExpectFailure(reason);
            f.RunStart();
            AssertFailure(s.Request, s.Battle, reason);
            if (em.HasComponent<BattleRNG>(s.Battle))
            {
                BattleRNG after = em.GetComponentData<BattleRNG>(s.Battle);
                Assert.That(after.StateA, Is.EqualTo(before.StateA));
                Assert.That(after.StateB, Is.EqualTo(before.StateB));
            }
        }

        private void AssertFailure(
            Entity request,
            Entity battle,
            BattleStartFailureReason reason,
            BattlePhase expectedPhase = BattlePhase.Setup
        )
        {
            Assert.That(em.Exists(request), Is.True, "Failed setup must stay inspectable.");
            Assert.That(em.HasComponent<BattleStartRequest>(request), Is.False);
            Assert.That(em.HasComponent<BattleStartFailed>(request), Is.True);
            BattleStartFailed failure = em.GetComponentData<BattleStartFailed>(request);
            Assert.That(failure.Reason, Is.EqualTo(reason));
            Assert.That(failure.Battle, Is.EqualTo(battle));
            Assert.That(em.GetComponentData<BattleState>(battle).Phase, Is.EqualTo(expectedPhase));
            Assert.That(em.HasComponent<ActiveTurnPlayer>(battle), Is.False);
            Assert.That(
                em.HasComponent<BattleReadyTag>(battle),
                Is.False,
                "Rejected starts must never request a phase transition."
            );
        }

        private static void ExpectFailure(BattleStartFailureReason reason)
        {
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\[BattleStart\] FAILED.*Reason=" + reason + @"\b")
            );
        }
    }
}
