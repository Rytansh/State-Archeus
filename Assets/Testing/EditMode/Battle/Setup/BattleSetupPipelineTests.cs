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
using Archeus.Battle.Components.Turns;
using Archeus.Core.Debugging;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Archeus.Testing.Battle.Setup
{
    /// <summary>
    /// Assertions at EVERY handoff, using the five production systems.
    /// Deliberately stops at TurnStart: turnflow is a separate test boundary.
    /// </summary>
    public class BattleSetupPipelineTests
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
        public void EntirePipeline_PreservesCorrectStateAtEveryHandoff()
        {
            var s = f.CreateRequest();
            AssertInitialRequest(s);

            f.RunCreation();
            s = f.AfterCreation(s);
            AssertAfterCreation(s);

            f.RunRoster();
            AssertAfterRoster(s);

            BattleRNG beforeReadiness = em.GetComponentData<BattleRNG>(s.Battle);
            f.RunReadiness();
            AssertAfterReadiness(s);
            AssertSameRng(s.Battle, beforeReadiness);

            f.RunStart();
            AssertAfterStart(s, BattleSetupTestFixture.PlayerAID);
            AssertSameRng(s.Battle, beforeReadiness);

            f.RunPhaseTransition();
            AssertAfterPhaseTransition(s, BattleSetupTestFixture.PlayerAID);
            AssertSameRng(s.Battle, beforeReadiness);

            // One-shot: rerunning setup systems cannot manufacture extra entities.
            f.RunCreation();
            f.RunRoster();
            f.RunReadiness();
            f.RunStart();
            f.RunPhaseTransition();
            Assert.That(em.HasComponent<BattleReadyTag>(s.Battle), Is.False);
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.TurnStart)
            );
            Assert.That(f.Count<BattleTag>(), Is.EqualTo(1));
            Assert.That(f.Count<PlayerTag>(), Is.EqualTo(2));
            Assert.That(f.Count<CharacterTag>(), Is.EqualTo(4));
            Assert.That(f.Count<SkillTag>(), Is.EqualTo(2));
            Assert.That(f.Count<ActiveTurnPlayer>(), Is.EqualTo(1));
        }

        [Test]
        public void ReversedInputOrder_DoesNotChangeRosterRuntimeIdOrderOrInitiativeWinner()
        {
            var s = f.CreateRequest(battleID: 44, seed: 12345678, reversePlayerEntries: true);
            f.RunCreation();
            s = f.AfterCreation(s);
            AssertAfterCreation(s);
            f.RunRoster();
            AssertAfterRoster(s);
            f.RunReadiness();
            AssertAfterReadiness(s);
            f.RunStart();
            AssertAfterStart(s, BattleSetupTestFixture.PlayerAID);
            f.RunPhaseTransition();
            AssertAfterPhaseTransition(s, BattleSetupTestFixture.PlayerAID);
        }

        [Test]
        public void InvalidInitialPlayerIds_PreventCreationAndAllDownstreamStages()
        {
            var s = f.CreateRequest();
            var players = em.GetBuffer<BattlePlayerEntry>(s.Request);
            BattlePlayerEntry duplicated = players[1];
            duplicated.PlayerID = players[0].PlayerID;
            players[1] = duplicated;

            f.RunCreation();
            f.RunRoster();
            f.RunReadiness();
            f.RunStart();

            Assert.That(em.Exists(s.Request), Is.True);
            Assert.That(em.HasComponent<StartBattleRequest>(s.Request), Is.True);
            Assert.That(f.Count<BattleTag>(), Is.Zero);
            Assert.That(f.Count<BattleStartRequest>(), Is.Zero);
            Assert.That(f.Count<ActiveTurnPlayer>(), Is.Zero);
            // Current BattleCreationSystem has TODO rejection/diagnostics.
            // It leaves invalid requests pending; test records current behaviour.
        }

        [Test]
        public void InvalidRosterDefinition_StopsBeforeReadinessAndStart()
        {
            var s = f.CreateRequest();
            var deck = em.GetBuffer<PlayerDeckEntry>(s.Request);
            PlayerDeckEntry bad = deck[0];
            bad.CardDefinitionID = 999999; // Not in ContentLookupTables.
            deck[0] = bad;

            f.RunCreation();
            s = f.AfterCreation(s);
            f.RunRoster();
            Assert.That(em.HasComponent<RosterInitialisationFailed>(s.Request), Is.True);
            Assert.That(em.HasComponent<RosterReadyRequest>(s.Request), Is.False);
            Assert.That(f.Count<CharacterTag>(), Is.Zero);
            Assert.That(f.Count<SkillTag>(), Is.Zero);
            Assert.That(
                em.GetComponentData<BattleRuntimeIDCounter>(s.Battle).NextID,
                Is.EqualTo(BattleSetupTestFixture.FirstRuntimeID)
            );
            Assert.That(em.GetBuffer<BattleParticipant>(s.Battle).Length, Is.Zero);

            f.RunReadiness();
            f.RunStart();
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup)
            );
            Assert.That(em.HasComponent<BattleReadyTag>(s.Battle), Is.False);
            Assert.That(em.HasComponent<ActiveTurnPlayer>(s.Battle), Is.False);
            Assert.That(f.Count<BattleStartRequest>(), Is.Zero);
        }

        [Test]
        public void CorruptionBetweenRosterAndReadiness_PreventsStart()
        {
            var s = f.CreateRequest();
            f.RunCreation();
            s = f.AfterCreation(s);
            f.RunRoster();
            AssertAfterRoster(s);
            em.GetBuffer<BattleParticipant>(s.Battle).RemoveAt(0);

            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\[BattleReadiness\] FAILED.*Reason=ParticipantMismatch\b")
            );
            f.RunReadiness();

            Assert.That(em.HasComponent<BattleReadinessFailed>(s.Request), Is.True);
            Assert.That(
                em.GetComponentData<BattleReadinessFailed>(s.Request).Reason,
                Is.EqualTo(BattleReadinessFailureReason.ParticipantMismatch)
            );
            Assert.That(em.HasComponent<BattleStartRequest>(s.Request), Is.False);
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup)
            );

            f.RunStart();
            Assert.That(em.Exists(s.Request), Is.True);
            Assert.That(em.HasComponent<BattleReadyTag>(s.Battle), Is.False);
            Assert.That(em.HasComponent<ActiveTurnPlayer>(s.Battle), Is.False);
        }

        [Test]
        public void InvalidInitiativeAfterReadiness_PreventsStartWithoutConsumingSetupData()
        {
            var s = f.ReadyToStart();
            f.SetInitiative(f.Lead(s.PlayerA), float.NaN);
            BattleRNG before = em.GetComponentData<BattleRNG>(s.Battle);

            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\[BattleStart\] FAILED.*Reason=InvalidInitiative\b")
            );
            f.RunStart();

            Assert.That(em.HasComponent<BattleStartFailed>(s.Request), Is.True);
            Assert.That(
                em.GetComponentData<BattleStartFailed>(s.Request).Reason,
                Is.EqualTo(BattleStartFailureReason.InvalidInitiative)
            );
            Assert.That(em.HasBuffer<BattlePlayerEntry>(s.Request), Is.True);
            Assert.That(em.HasBuffer<PlayerDeckEntry>(s.Request), Is.True);
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup)
            );
            Assert.That(em.HasComponent<BattleReadyTag>(s.Battle), Is.False);
            Assert.That(em.HasComponent<ActiveTurnPlayer>(s.Battle), Is.False);
            AssertSameRng(s.Battle, before);
        }

        private void AssertInitialRequest(BattleSetupTestFixture.Scenario s)
        {
            Assert.That(em.HasComponent<StartBattleRequest>(s.Request), Is.True);
            Assert.That(em.GetBuffer<BattlePlayerEntry>(s.Request).Length, Is.EqualTo(2));
            Assert.That(em.GetBuffer<PlayerDeckEntry>(s.Request).Length, Is.EqualTo(4));
            Assert.That(f.Count<BattleTag>(), Is.Zero);
        }

        private void AssertAfterCreation(BattleSetupTestFixture.Scenario s)
        {
            Assert.That(em.Exists(s.Request), Is.True);
            Assert.That(em.HasComponent<StartBattleRequest>(s.Request), Is.False);
            Assert.That(em.HasComponent<InitialRosterConstructionRequest>(s.Request), Is.True);
            Assert.That(em.HasComponent<RosterReadyRequest>(s.Request), Is.False);
            Assert.That(em.GetBuffer<BattlePlayerEntry>(s.Request).Length, Is.EqualTo(2));
            Assert.That(em.GetBuffer<PlayerDeckEntry>(s.Request).Length, Is.EqualTo(4));

            Entity battle = s.Battle;
            Assert.That(em.HasComponent<BattleTag>(battle), Is.True);
            Assert.That(
                em.GetComponentData<BattleID>(battle).Value,
                Is.EqualTo(42UL).Or.EqualTo(44UL)
            );
            Assert.That(
                em.GetComponentData<BattleState>(battle).Phase,
                Is.EqualTo(BattlePhase.Setup)
            );
            Assert.That(em.HasComponent<BattleRNG>(battle), Is.True);
            Assert.That(em.HasComponent<BattleContentRegistry>(battle), Is.True);
            Assert.That(
                em.GetComponentData<BattleContentRegistry>(
                    battle
                ).BattleRegistryReference.IsCreated,
                Is.True
            );
            Assert.That(
                em.GetComponentData<BattleRuntimeIDCounter>(battle).NextID,
                Is.EqualTo(BattleSetupTestFixture.FirstRuntimeID)
            );
            Assert.That(em.GetComponentData<TurnCounter>(battle).CurrentTurn, Is.Zero);
            Assert.That(em.HasComponent<BattleReadyTag>(battle), Is.False);
            Assert.That(
                em.HasComponent<ActiveTurnPlayer>(battle),
                Is.False,
                "Creation must not assign the active player before Initiative selection."
            );
            Assert.That(em.GetBuffer<BattleParticipant>(battle).Length, Is.Zero);
            Assert.That(em.GetBuffer<BattlePlayerReference>(battle).Length, Is.EqualTo(2));
            Assert.That(em.HasBuffer<BattleEvent>(battle), Is.True);
            Assert.That(em.HasBuffer<ChainedBattleEvent>(battle), Is.True);
            Assert.That(
                em.HasBuffer<Archeus.Battle.Buffers.Actions.ActionExecutionRequest>(battle),
                Is.True
            );

            Assert.That(f.Count<BattleTag>(), Is.EqualTo(1));
            Assert.That(f.Count<PlayerTag>(), Is.EqualTo(2));
            Assert.That(f.Count<CharacterTag>(), Is.Zero);
            Assert.That(f.Count<SkillTag>(), Is.Zero);
            var sides = new HashSet<BattleSide>();
            foreach (Entity player in new[] { s.PlayerA, s.PlayerB })
            {
                Assert.That(em.GetComponentData<OwnedBattle>(player).Battle, Is.EqualTo(battle));
                Assert.That(em.HasComponent<LeadCharacter>(player), Is.True);
                Assert.That(
                    em.GetComponentData<LeadCharacter>(player).Value,
                    Is.EqualTo(Entity.Null)
                );
                Assert.That(em.GetBuffer<DeckCard>(player).Length, Is.Zero);
                Assert.That(em.GetBuffer<HandCard>(player).Length, Is.Zero);
                Assert.That(em.GetBuffer<FieldCard>(player).Length, Is.Zero);
                Assert.That(em.GetComponentData<MaxActionPoints>(player).Value, Is.EqualTo(4));
                Assert.That(
                    em.GetComponentData<RemainingActionPoints>(player).Value,
                    Is.EqualTo(4)
                );
                Assert.That(em.GetComponentData<MaxHandSize>(player).Value, Is.EqualTo(4));
                sides.Add(em.GetComponentData<BattleSideAssignment>(player).Side);
            }
            Assert.That(sides.SetEquals(new[] { BattleSide.SideA, BattleSide.SideB }), Is.True);
        }

        private void AssertAfterRoster(BattleSetupTestFixture.Scenario s)
        {
            Assert.That(em.Exists(s.Request), Is.True);
            Assert.That(em.HasComponent<InitialRosterConstructionRequest>(s.Request), Is.False);
            Assert.That(em.HasComponent<RosterReadyRequest>(s.Request), Is.True);
            Assert.That(em.HasComponent<RosterInitialisationFailed>(s.Request), Is.False);
            Assert.That(em.HasComponent<BattleStartRequest>(s.Request), Is.False);
            Assert.That(em.GetBuffer<BattlePlayerEntry>(s.Request).Length, Is.EqualTo(2));
            Assert.That(em.GetBuffer<PlayerDeckEntry>(s.Request).Length, Is.EqualTo(4));
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup)
            );
            Assert.That(em.HasComponent<BattleReadyTag>(s.Battle), Is.False);
            Assert.That(em.HasComponent<ActiveTurnPlayer>(s.Battle), Is.False);
            Assert.That(f.Count<CharacterTag>(), Is.EqualTo(4));
            Assert.That(f.Count<SkillTag>(), Is.EqualTo(2));
            Assert.That(
                em.GetComponentData<BattleRuntimeIDCounter>(s.Battle).NextID,
                Is.EqualTo(106U)
            );

            // Sorted logical player ID, all Leads first; then each player's deck.
            AssertPlayerRoster(
                s,
                s.PlayerA,
                BattleSetupTestFixture.LeadAID,
                BattleSetupTestFixture.DeckCharacterAID,
                BattleSetupTestFixture.SkillAID,
                100,
                102,
                103
            );
            AssertPlayerRoster(
                s,
                s.PlayerB,
                BattleSetupTestFixture.LeadBID,
                BattleSetupTestFixture.DeckCharacterBID,
                BattleSetupTestFixture.SkillBID,
                101,
                104,
                105
            );
            var participants = em.GetBuffer<BattleParticipant>(s.Battle);
            Assert.That(participants.Length, Is.EqualTo(2));
            Assert.That(
                new HashSet<Entity>
                {
                    participants[0].Participant,
                    participants[1].Participant,
                }.SetEquals(new[] { f.Lead(s.PlayerA), f.Lead(s.PlayerB) }),
                Is.True
            );
        }

        private void AssertPlayerRoster(
            BattleSetupTestFixture.Scenario s,
            Entity player,
            uint leadDefinition,
            uint characterDefinition,
            uint skillDefinition,
            uint leadRuntimeId,
            uint characterRuntimeId,
            uint skillRuntimeId
        )
        {
            Entity lead = f.Lead(player);
            Entity character = f.DeckCharacter(player);
            Entity skill = f.DeckSkill(player);
            Assert.That(em.GetBuffer<FieldCard>(player).Length, Is.EqualTo(1));
            Assert.That(em.GetBuffer<FieldCard>(player)[0].Card, Is.EqualTo(lead));
            Assert.That(
                em.GetBuffer<FieldCard>(player)[0].Position,
                Is.EqualTo(FieldPosition.Lead)
            );
            Assert.That(em.GetBuffer<DeckCard>(player).Length, Is.EqualTo(2));
            Assert.That(em.GetBuffer<HandCard>(player).Length, Is.Zero);

            AssertCard(s, player, lead, leadDefinition, leadRuntimeId, isSkill: false);
            AssertCard(
                s,
                player,
                character,
                characterDefinition,
                characterRuntimeId,
                isSkill: false
            );
            AssertCard(s, player, skill, skillDefinition, skillRuntimeId, isSkill: true);
            Assert.That(em.HasComponent<CharacterRuntimeStatBaseline>(lead), Is.True);
            Assert.That(em.HasComponent<CharacterRuntimeStatBaseline>(character), Is.True);
            Assert.That(em.HasComponent<SkillRuntimeStatBaseline>(skill), Is.True);
            Assert.That(em.HasComponent<CurrentHealth>(lead), Is.True);
            float leadInitiative = player == s.PlayerA ? 100f : 50f;
            float deckInitiative = player == s.PlayerA ? 250f : 150f;
            var leadStats = em.GetComponentData<CharacterRuntimeStatBaseline>(lead);
            var deckStats = em.GetComponentData<CharacterRuntimeStatBaseline>(character);
            var skillStats = em.GetComponentData<SkillRuntimeStatBaseline>(skill);
            Assert.That(leadStats.Initiative, Is.EqualTo(leadInitiative));
            Assert.That(deckStats.Initiative, Is.EqualTo(deckInitiative));
            Assert.That(leadStats.Attack, Is.EqualTo(player == s.PlayerA ? 101f : 102f));
            Assert.That(deckStats.Attack, Is.EqualTo(player == s.PlayerA ? 103f : 104f));
            Assert.That(skillStats.Attack, Is.EqualTo(player == s.PlayerA ? 301f : 302f));
            Assert.That(
                em.GetComponentData<CurrentHealth>(lead).Value,
                Is.EqualTo(leadStats.MaxHealth)
            );
            Assert.That(em.GetBuffer<BehaviourReference>(lead).Length, Is.Zero);
            Assert.That(em.GetBuffer<BehaviourRuntimeState>(lead).Length, Is.Zero);
            Assert.That(em.GetBuffer<ActiveEffect>(lead).Length, Is.Zero);
        }

        private void AssertCard(
            BattleSetupTestFixture.Scenario s,
            Entity player,
            Entity card,
            uint definition,
            uint runtimeID,
            bool isSkill
        )
        {
            Assert.That(em.Exists(card), Is.True);
            Assert.That(em.GetComponentData<CardDefinitionID>(card).Value, Is.EqualTo(definition));
            Assert.That(em.GetComponentData<CardRuntimeID>(card).Value, Is.EqualTo(runtimeID));
            Assert.That(em.GetComponentData<CardOwner>(card).Player, Is.EqualTo(player));
            Assert.That(em.GetComponentData<OwnedBattle>(card).Battle, Is.EqualTo(s.Battle));
            Assert.That(
                em.GetComponentData<BattleSideAssignment>(card).Side,
                Is.EqualTo(em.GetComponentData<BattleSideAssignment>(player).Side)
            );
            Assert.That(em.HasComponent<SkillTag>(card), Is.EqualTo(isSkill));
            Assert.That(em.HasComponent<CharacterTag>(card), Is.EqualTo(!isSkill));
        }

        private void AssertAfterReadiness(BattleSetupTestFixture.Scenario s)
        {
            Assert.That(em.Exists(s.Request), Is.True);
            Assert.That(em.HasComponent<RosterReadyRequest>(s.Request), Is.False);
            Assert.That(em.HasComponent<BattleReadinessFailed>(s.Request), Is.False);
            Assert.That(em.HasComponent<BattleStartRequest>(s.Request), Is.True);
            Assert.That(
                em.GetComponentData<BattleStartRequest>(s.Request).Battle,
                Is.EqualTo(s.Battle)
            );
            Assert.That(em.GetBuffer<BattlePlayerEntry>(s.Request).Length, Is.EqualTo(2));
            Assert.That(em.GetBuffer<PlayerDeckEntry>(s.Request).Length, Is.EqualTo(4));
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup)
            );
            Assert.That(em.HasComponent<BattleReadyTag>(s.Battle), Is.False);
            Assert.That(em.HasComponent<ActiveTurnPlayer>(s.Battle), Is.False);
            Assert.That(em.GetComponentData<TurnCounter>(s.Battle).CurrentTurn, Is.Zero);
            Assert.That(f.Count<CharacterTag>(), Is.EqualTo(4));
            Assert.That(f.Count<SkillTag>(), Is.EqualTo(2));
        }

        private void AssertAfterStart(BattleSetupTestFixture.Scenario s, ulong playerID)
        {
            Assert.That(
                em.Exists(s.Request),
                Is.False,
                "Temporary setup entity should be destroyed only after successful start."
            );
            Assert.That(f.Count<StartBattleRequest>(), Is.Zero);
            Assert.That(f.Count<InitialRosterConstructionRequest>(), Is.Zero);
            Assert.That(f.Count<RosterReadyRequest>(), Is.Zero);
            Assert.That(f.Count<BattleStartRequest>(), Is.Zero);
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.Setup),
                "BattleStartSystem must not transition the phase directly."
            );
            Assert.That(
                em.HasComponent<BattleReadyTag>(s.Battle),
                Is.True,
                "BattleStartSystem must signal readiness to the phase-transition system."
            );
            Assert.That(em.HasComponent<ActiveTurnPlayer>(s.Battle), Is.True);
            Assert.That(f.ActivePlayerID(s), Is.EqualTo(playerID));
            Assert.That(
                em.GetComponentData<TurnCounter>(s.Battle).CurrentTurn,
                Is.Zero,
                "Only TurnStartSystem advances the turn counter."
            );
            Assert.That(em.HasComponent<SkipPlanningStageTag>(s.PlayerA), Is.False);
            Assert.That(em.HasComponent<SkipPlanningStageTag>(s.PlayerB), Is.False);
            Assert.That(em.HasComponent<SkipAttackingStageTag>(s.PlayerA), Is.False);
            Assert.That(em.HasComponent<SkipAttackingStageTag>(s.PlayerB), Is.False);
            Assert.That(f.Count<CharacterTag>(), Is.EqualTo(4));
            Assert.That(f.Count<SkillTag>(), Is.EqualTo(2));
            Assert.That(em.GetBuffer<BattleParticipant>(s.Battle).Length, Is.EqualTo(2));
        }

        private void AssertAfterPhaseTransition(BattleSetupTestFixture.Scenario s, ulong playerID)
        {
            Assert.That(
                em.GetComponentData<BattleState>(s.Battle).Phase,
                Is.EqualTo(BattlePhase.TurnStart)
            );
            Assert.That(
                em.HasComponent<BattleReadyTag>(s.Battle),
                Is.False,
                "The phase transition must consume BattleReadyTag."
            );
            Assert.That(em.HasComponent<ActiveTurnPlayer>(s.Battle), Is.True);
            Assert.That(f.ActivePlayerID(s), Is.EqualTo(playerID));
            Assert.That(
                em.GetComponentData<TurnCounter>(s.Battle).CurrentTurn,
                Is.Zero,
                "Only TurnStartSystem advances the turn counter."
            );
            Assert.That(em.Exists(s.Request), Is.False);
        }

        private void AssertSameRng(Entity battle, BattleRNG rng)
        {
            var current = em.GetComponentData<BattleRNG>(battle);
            Assert.That(current.StateA, Is.EqualTo(rng.StateA));
            Assert.That(current.StateB, Is.EqualTo(rng.StateB));
        }
    }
}
