using System.Collections.Generic;
using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Setup
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleInitialisationGroup))]
    [UpdateAfter(typeof(BattleReadinessSystem))]
    public partial struct BattleStartSystem : ISystem
    {
        private struct PlayerInitiative
        {
            public Entity Player;
            public ulong PlayerID;
            public BattleSide Side;
            public double Lead;
            public double Total;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleStartRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityManager entities = state.EntityManager;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var requestCounts = new Dictionary<Entity, int>();
            var messages = new List<(bool Error, string Text)>();

            // A duplicated start request must not start one battle twice.
            foreach (
                RefRO<BattleStartRequest> request in SystemAPI.Query<RefRO<BattleStartRequest>>()
            )
            {
                Entity battle = request.ValueRO.Battle;
                requestCounts.TryGetValue(battle, out int count);
                requestCounts[battle] = count + 1;
            }

            foreach (
                var (request, requestEntity) in SystemAPI
                    .Query<RefRO<BattleStartRequest>>()
                    .WithEntityAccess()
            )
            {
                Entity battle = request.ValueRO.Battle;

                if (requestCounts[battle] != 1)
                {
                    Reject(
                        entities,
                        ecb,
                        requestEntity,
                        battle,
                        BattleStartFailureReason.DuplicateStartRequest,
                        messages
                    );
                    continue;
                }

                // The setup entity must have handed off cleanly from readiness.
                if (
                    entities.HasComponent<RosterReadyRequest>(requestEntity)
                    || entities.HasComponent<InitialRosterConstructionRequest>(requestEntity)
                    || entities.HasComponent<RosterInitialisationFailed>(requestEntity)
                    || entities.HasComponent<BattleReadinessFailed>(requestEntity)
                    || entities.HasComponent<StartBattleRequest>(requestEntity)
                    || entities.HasComponent<BattleStartFailed>(requestEntity)
                )
                {
                    Reject(
                        entities,
                        ecb,
                        requestEntity,
                        battle,
                        BattleStartFailureReason.InvalidSetupRequest,
                        messages
                    );
                    continue;
                }

                if (!IsBattleReadyForStart(entities, battle))
                {
                    Reject(
                        entities,
                        ecb,
                        requestEntity,
                        battle,
                        BattleStartFailureReason.InvalidBattle,
                        messages
                    );
                    continue;
                }

                DynamicBuffer<BattlePlayerReference> references =
                    entities.GetBuffer<BattlePlayerReference>(battle);

                if (
                    references.Length != 2
                    || !TryReadPlayer(
                        entities,
                        battle,
                        references[0].Player,
                        out PlayerInitiative first
                    )
                    || !TryReadPlayer(
                        entities,
                        battle,
                        references[1].Player,
                        out PlayerInitiative second
                    )
                    || first.Player == second.Player
                    || first.PlayerID == second.PlayerID
                    || first.Side == second.Side
                )
                {
                    Reject(
                        entities,
                        ecb,
                        requestEntity,
                        battle,
                        BattleStartFailureReason.InvalidPlayers,
                        messages
                    );
                    continue;
                }

                // Canonical order matters for reproducible random tiebreaks.
                if (first.PlayerID > second.PlayerID)
                {
                    (first, second) = (second, first);
                }

                if (
                    !TryCalculateInitiative(entities, battle, ref first)
                    || !TryCalculateInitiative(entities, battle, ref second)
                )
                {
                    Reject(
                        entities,
                        ecb,
                        requestEntity,
                        battle,
                        BattleStartFailureReason.InvalidInitiative,
                        messages
                    );
                    continue;
                }

                Entity firstPlayer;
                bool completeTie = false;
                string decidingRule;

                if (first.Total > second.Total)
                {
                    firstPlayer = first.Player;
                    decidingRule = "TotalInitiative";
                }
                else if (second.Total > first.Total)
                {
                    firstPlayer = second.Player;
                    decidingRule = "TotalInitiative";
                }
                else if (first.Lead > second.Lead)
                {
                    firstPlayer = first.Player;
                    decidingRule = "LeadInitiative";
                }
                else if (second.Lead > first.Lead)
                {
                    firstPlayer = second.Player;
                    decidingRule = "LeadInitiative";
                }
                else
                {
                    completeTie = true;
                    decidingRule = "DeterministicRNG";

                    BattleRNG rng = entities.GetComponentData<BattleRNG>(battle);
                    int winningIndex = BattleRNGService.RollInt(ref rng, 0, 2);
                    firstPlayer = winningIndex == 0 ? first.Player : second.Player;
                    ecb.SetComponent(battle, rng); // Advance the battle's RNG ONLY on a full tie.
                }

                // Authoritative current-turn ownership is battle-scoped.
                ecb.AddComponent(battle, new ActiveTurnPlayer { Player = firstPlayer });

                if (entities.HasComponent<TurnCounter>(battle))
                {
                    ecb.SetComponent(battle, new TurnCounter { CurrentTurn = 0 });
                }
                else
                {
                    ecb.AddComponent(battle, new TurnCounter { CurrentTurn = 0 });
                }

                if (completeTie)
                {
                    ecb.AddComponent<SkipPlanningStageTag>(first.Player);
                    ecb.AddComponent<SkipAttackingStageTag>(first.Player);
                    ecb.AddComponent<SkipPlanningStageTag>(second.Player);
                    ecb.AddComponent<SkipAttackingStageTag>(second.Player);
                }

                // TurnStartSystem will increment CurrentTurn to 1.
                ecb.AddComponent<BattleReadyTag>(battle);

                // Original setup buffers are no longer needed after successful start.
                ecb.DestroyEntity(requestEntity);

                ulong chosenID = firstPlayer == first.Player ? first.PlayerID : second.PlayerID;
                messages.Add(
                    (
                        false,
                        $"[BattleStart] STARTED | Battle={battle.Index}:{battle.Version} | "
                            + $"FirstPlayerID={chosenID} | "
                            + $"Totals={first.PlayerID}:{first.Total}, {second.PlayerID}:{second.Total} | "
                            + $"Leads={first.Lead}/{second.Lead} | Decision={decidingRule} | "
                            + $"SkipFirstOffensiveTurns={completeTie}"
                    )
                );
            }

            ecb.Playback(entities);

            foreach (var message in messages)
            {
                if (message.Error)
                    Logging.Error(LogCategory.Setup, message.Text);
                else
                    Logging.Info(LogCategory.Setup, message.Text);
            }
        }

        private static bool IsBattleReadyForStart(EntityManager entities, Entity battle)
        {
            if (
                !entities.Exists(battle)
                || !entities.HasComponent<BattleTag>(battle)
                || !entities.HasComponent<BattleState>(battle)
                || !entities.HasComponent<BattleRNG>(battle)
                || !entities.HasBuffer<BattlePlayerReference>(battle)
                || entities.HasComponent<ActiveTurnPlayer>(battle)
            )
            {
                return false;
            }

            if (entities.GetComponentData<BattleState>(battle).Phase != BattlePhase.Setup)
                return false;

            return !entities.HasComponent<TurnCounter>(battle)
                || entities.GetComponentData<TurnCounter>(battle).CurrentTurn == 0;
        }

        private static bool TryReadPlayer(
            EntityManager entities,
            Entity battle,
            Entity player,
            out PlayerInitiative result
        )
        {
            result = default;

            if (
                !entities.Exists(player)
                || !entities.HasComponent<PlayerTag>(player)
                || !entities.HasComponent<BattlePlayerIdentity>(player)
                || !entities.HasComponent<OwnedBattle>(player)
                || !entities.HasComponent<BattleSideAssignment>(player)
                || !entities.HasComponent<LeadCharacter>(player)
                || !entities.HasBuffer<DeckCard>(player)
                || entities.GetComponentData<OwnedBattle>(player).Battle != battle
            )
            {
                return false;
            }

            BattleSide side = entities.GetComponentData<BattleSideAssignment>(player).Side;

            if (side != BattleSide.SideA && side != BattleSide.SideB)
                return false;

            result = new PlayerInitiative
            {
                Player = player,
                PlayerID = entities.GetComponentData<BattlePlayerIdentity>(player).PlayerID,
                Side = side,
            };

            return true;
        }

        private static bool TryCalculateInitiative(
            EntityManager entities,
            Entity battle,
            ref PlayerInitiative player
        )
        {
            Entity lead = entities.GetComponentData<LeadCharacter>(player.Player).Value;
            if (
                !TryReadCharacterInitiative(
                    entities,
                    battle,
                    player.Player,
                    lead,
                    out double leadInitiative
                )
            )
                return false;

            double total = leadInitiative;
            DynamicBuffer<DeckCard> deck = entities.GetBuffer<DeckCard>(player.Player);
            foreach (DeckCard entry in deck)
            {
                Entity card = entry.Card;
                if (!entities.Exists(card) || card == lead)
                    return false;

                // Skill cards do not contribute to opening Initiative.
                if (entities.HasComponent<SkillTag>(card))
                {
                    if (entities.HasComponent<CharacterTag>(card))
                        return false;
                    continue;
                }

                if (
                    !TryReadCharacterInitiative(
                        entities,
                        battle,
                        player.Player,
                        card,
                        out double initiative
                    )
                )
                    return false;

                total += initiative;
            }

            if (double.IsNaN(total) || double.IsInfinity(total))
                return false;

            player.Lead = leadInitiative;
            player.Total = total;
            return true;
        }

        private static bool TryReadCharacterInitiative(
            EntityManager entities,
            Entity battle,
            Entity owner,
            Entity character,
            out double initiative
        )
        {
            initiative = 0;
            if (
                !entities.Exists(character)
                || !entities.HasComponent<CharacterTag>(character)
                || entities.HasComponent<SkillTag>(character)
                || !entities.HasComponent<CharacterRuntimeStatBaseline>(character)
                || !entities.HasComponent<CardOwner>(character)
                || !entities.HasComponent<OwnedBattle>(character)
                || entities.GetComponentData<CardOwner>(character).Player != owner
                || entities.GetComponentData<OwnedBattle>(character).Battle != battle
            )
            {
                return false;
            }

            initiative = entities
                .GetComponentData<CharacterRuntimeStatBaseline>(character)
                .Initiative;
            return !double.IsNaN(initiative) && !double.IsInfinity(initiative);
        }

        private static void Reject(
            EntityManager entities,
            EntityCommandBuffer ecb,
            Entity requestEntity,
            Entity battle,
            BattleStartFailureReason reason,
            List<(bool Error, string Text)> messages
        )
        {
            ecb.RemoveComponent<BattleStartRequest>(requestEntity);
            var failure = new BattleStartFailed { Battle = battle, Reason = reason };
            if (entities.HasComponent<BattleStartFailed>(requestEntity))
                ecb.SetComponent(requestEntity, failure);
            else
                ecb.AddComponent(requestEntity, failure);
            messages.Add(
                (
                    true,
                    $"[BattleStart] FAILED | Battle={battle.Index}:{battle.Version} | "
                        + $"Request={requestEntity.Index}:{requestEntity.Version} | "
                        + $"Reason={reason} | TurnStart blocked"
                )
            );
        }
    }
}
