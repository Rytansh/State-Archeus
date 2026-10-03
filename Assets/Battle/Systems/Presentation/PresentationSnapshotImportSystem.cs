using System;
using System.Collections.Generic;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Presentation.State;
using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup), OrderFirst = true)]
    [UpdateAfter(typeof(PresentationFactImportSystem))]
    public partial class PresentationSnapshotImportSystem : SystemBase
    {
        private EntityQuery bridgeQuery;
        private EntityQuery battleStateQuery;
        private EntityQuery cardStateQuery;
        private EntityQuery handStateQuery;
        private EntityQuery playerStateQuery;
        private EntityQuery characterStateQuery;
        private EntityQuery mirrorRevisionQuery;

        protected override void OnCreate()
        {
            bridgeQuery = GetEntityQuery(
                ComponentType.ReadOnly<BattlePresentationBridgeReference>()
            );

            battleStateQuery = GetEntityQuery(ComponentType.ReadWrite<BattlePresentationState>());

            characterStateQuery = GetEntityQuery(
                ComponentType.ReadWrite<CharacterPresentationState>()
            );
            mirrorRevisionQuery = GetEntityQuery(
                ComponentType.ReadWrite<PresentationMirrorRevision>()
            );

            playerStateQuery = GetEntityQuery(ComponentType.ReadWrite<PlayerPresentationState>());

            cardStateQuery = GetEntityQuery(ComponentType.ReadWrite<CardPresentationState>());

            handStateQuery = GetEntityQuery(ComponentType.ReadWrite<HandPresentationState>());

            RequireForUpdate(bridgeQuery);
        }

        protected override void OnUpdate()
        {
            Entity bridgeEntity = bridgeQuery.GetSingletonEntity();

            BattlePresentationBridgeReference bridgeReference =
                EntityManager.GetComponentObject<BattlePresentationBridgeReference>(bridgeEntity);

            BattlePresentationBridge bridge = bridgeReference.Bridge;

            if (!bridge.TryTakeLatestStateSnapshot(out PresentationStateSnapshot snapshot))
            {
                return;
            }

            if (snapshot == null)
            {
                return;
            }

            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(PresentationStateSnapshot snapshot)
        {
            CharacterPresentationState[] characters =
                snapshot.Characters ?? Array.Empty<CharacterPresentationState>();

            PlayerPresentationState[] players =
                snapshot.Players ?? Array.Empty<PlayerPresentationState>();

            CardPresentationState[] cards = snapshot.Cards ?? Array.Empty<CardPresentationState>();

            HandPresentationState[] hands = snapshot.Hands ?? Array.Empty<HandPresentationState>();

            if (
                !ValidatePlayerSnapshot(players)
                || !ValidateCharacterSnapshot(characters)
                || !ValidateCardSnapshot(cards)
                || !ValidateHandSnapshot(hands)
            )
            {
                return;
            }

            if (TryGetMirrorRevision(out PresentationMirrorRevision currentRevision))
            {
                if (
                    currentRevision.BattleRuntimeID == snapshot.Battle.BattleRuntimeID
                    && snapshot.Revision <= currentRevision.Value
                )
                {
                    Logging.Warn(
                        LogCategory.Presentation,
                        $"[STATE SNAPSHOT IMPORT] "
                            + $"Ignoring stale snapshot | "
                            + $"Battle={snapshot.Battle.BattleRuntimeID} | "
                            + $"IncomingRevision={snapshot.Revision} | "
                            + $"CurrentRevision={currentRevision.Value}"
                    );

                    return;
                }
            }

            if (
                TryGetCurrentBattleState(out BattlePresentationState currentBattle)
                && currentBattle.BattleRuntimeID != snapshot.Battle.BattleRuntimeID
            )
            {
                ClearStateMirror();
            }

            ApplyBattleState(snapshot.Battle);

            ApplyPlayerStates(players);
            ApplyCharacterStates(characters);
            ApplyCardStates(cards);
            ApplyHandStates(hands);

            ApplyMirrorRevision(snapshot.Battle.BattleRuntimeID, snapshot.Revision);

            Logging.Info(
                LogCategory.Presentation,
                $"[STATE SNAPSHOT IMPORT] "
                    + $"Battle={snapshot.Battle.BattleRuntimeID} | "
                    + $"Revision={snapshot.Revision} | "
                    + $"Phase={snapshot.Battle.Phase} | "
                    + $"Turn={snapshot.Battle.TurnNumber} | "
                    + $"Characters={characters.Length}"
            );
        }

        private bool TryGetMirrorRevision(out PresentationMirrorRevision mirrorRevision)
        {
            using NativeArray<Entity> entities = mirrorRevisionQuery.ToEntityArray(Allocator.Temp);

            if (entities.Length == 0)
            {
                mirrorRevision = default;

                return false;
            }

            if (entities.Length > 1)
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE SNAPSHOT IMPORT] Expected at most one "
                        + $"PresentationMirrorRevision, found "
                        + $"{entities.Length}."
                );

                mirrorRevision = default;

                return false;
            }

            mirrorRevision = EntityManager.GetComponentData<PresentationMirrorRevision>(
                entities[0]
            );

            return true;
        }

        private void ApplyMirrorRevision(ulong battleRuntimeID, ulong revision)
        {
            using NativeArray<Entity> entities = mirrorRevisionQuery.ToEntityArray(Allocator.Temp);

            PresentationMirrorRevision mirrorRevision = new PresentationMirrorRevision
            {
                BattleRuntimeID = battleRuntimeID,
                Value = revision,
                Status = PresentationMirrorSyncStatus.Synchronized,
            };

            if (entities.Length == 0)
            {
                Entity entity = EntityManager.CreateEntity();
                EntityManager.SetName(entity, "Presentation Mirror Revision");
                EntityManager.AddComponentData(entity, mirrorRevision);
                return;
            }

            EntityManager.SetComponentData(entities[0], mirrorRevision);
        }

        private bool TryGetCurrentBattleState(out BattlePresentationState battleState)
        {
            using NativeArray<Entity> entities = battleStateQuery.ToEntityArray(Allocator.Temp);

            if (entities.Length == 0)
            {
                battleState = default;

                return false;
            }

            if (entities.Length > 1)
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE SNAPSHOT IMPORT] PresentationWorld contains "
                        + $"{entities.Length} BattlePresentationState "
                        + "entities. Expected at most one."
                );
            }

            battleState = EntityManager.GetComponentData<BattlePresentationState>(entities[0]);

            return true;
        }

        private void ApplyBattleState(in BattlePresentationState state)
        {
            using NativeArray<Entity> entities = battleStateQuery.ToEntityArray(Allocator.Temp);

            if (entities.Length == 0)
            {
                Entity battleEntity = EntityManager.CreateEntity();

                EntityManager.SetName(battleEntity, "Battle Presentation State");

                EntityManager.AddComponentData(battleEntity, state);

                return;
            }

            EntityManager.SetComponentData(entities[0], state);
        }

        private void ApplyCharacterStates(CharacterPresentationState[] incomingStates)
        {
            Dictionary<uint, Entity> existingCharacters = new();

            using (NativeArray<Entity> entities = characterStateQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];

                    CharacterPresentationState existingState =
                        EntityManager.GetComponentData<CharacterPresentationState>(entity);

                    if (!existingCharacters.TryAdd(existingState.RuntimeID, entity))
                    {
                        Logging.Error(
                            LogCategory.Presentation,
                            $"[STATE SNAPSHOT IMPORT] Duplicate "
                                + $"presentation character RuntimeID="
                                + $"{existingState.RuntimeID}."
                        );
                    }
                }
            }

            HashSet<uint> incomingRuntimeIDs = new();

            for (int i = 0; i < incomingStates.Length; i++)
            {
                CharacterPresentationState incoming = incomingStates[i];

                incomingRuntimeIDs.Add(incoming.RuntimeID);

                if (existingCharacters.TryGetValue(incoming.RuntimeID, out Entity existingEntity))
                {
                    EntityManager.SetComponentData(existingEntity, incoming);

                    CharacterVisibleState visibleState = new CharacterVisibleState
                    {
                        Health = incoming.CurrentHealth,
                        IsAlive = incoming.IsAlive,
                    };

                    if (EntityManager.HasComponent<CharacterVisibleState>(existingEntity))
                    {
                        EntityManager.SetComponentData(existingEntity, visibleState);
                    }
                    else
                    {
                        EntityManager.AddComponentData(existingEntity, visibleState);
                    }

                    continue;
                }

                Entity characterEntity = EntityManager.CreateEntity();

                EntityManager.SetName(
                    characterEntity,
                    $"Character Presentation State {incoming.RuntimeID}"
                );

                EntityManager.AddComponentData(characterEntity, incoming);

                EntityManager.AddComponentData(
                    characterEntity,
                    new CharacterVisibleState
                    {
                        Health = incoming.CurrentHealth,
                        IsAlive = incoming.IsAlive,
                    }
                );
            }

            /*
             * A full snapshot describes the complete
             * authoritative presentation mirror.
             *
             * Existing mirror characters absent from the
             * snapshot are therefore stale.
             */
            foreach (KeyValuePair<uint, Entity> existing in existingCharacters)
            {
                if (incomingRuntimeIDs.Contains(existing.Key))
                {
                    continue;
                }

                if (EntityManager.Exists(existing.Value))
                {
                    EntityManager.DestroyEntity(existing.Value);
                }
            }
        }

        private void ApplyPlayerStates(PlayerPresentationState[] incomingStates)
        {
            Dictionary<BattleSide, Entity> existingPlayers = new();

            using (NativeArray<Entity> entities = playerStateQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];

                    PlayerPresentationState existing =
                        EntityManager.GetComponentData<PlayerPresentationState>(entity);

                    if (!existingPlayers.TryAdd(existing.Side, entity))
                    {
                        Logging.Error(
                            LogCategory.Presentation,
                            $"[STATE SNAPSHOT IMPORT] Duplicate "
                                + $"presentation player Side="
                                + $"{existing.Side}."
                        );
                    }
                }
            }

            HashSet<BattleSide> incomingSides = new();

            for (int i = 0; i < incomingStates.Length; i++)
            {
                PlayerPresentationState incoming = incomingStates[i];

                incomingSides.Add(incoming.Side);

                if (existingPlayers.TryGetValue(incoming.Side, out Entity existingEntity))
                {
                    EntityManager.SetComponentData(existingEntity, incoming);

                    continue;
                }

                Entity entity = EntityManager.CreateEntity();

                EntityManager.SetName(entity, $"Player Presentation State {incoming.Side}");

                EntityManager.AddComponentData(entity, incoming);
            }

            foreach (KeyValuePair<BattleSide, Entity> existing in existingPlayers)
            {
                if (incomingSides.Contains(existing.Key))
                {
                    continue;
                }

                if (EntityManager.Exists(existing.Value))
                {
                    EntityManager.DestroyEntity(existing.Value);
                }
            }
        }

        private void ApplyCardStates(CardPresentationState[] incomingStates)
        {
            Dictionary<uint, Entity> existingCards = new();

            using (NativeArray<Entity> entities = cardStateQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];

                    CardPresentationState existing =
                        EntityManager.GetComponentData<CardPresentationState>(entity);

                    if (!existingCards.TryAdd(existing.RuntimeID, entity))
                    {
                        Logging.Error(
                            LogCategory.Presentation,
                            $"[STATE SNAPSHOT IMPORT] Duplicate "
                                + $"presentation card RuntimeID="
                                + $"{existing.RuntimeID}."
                        );
                    }
                }
            }

            HashSet<uint> incomingRuntimeIDs = new();

            for (int i = 0; i < incomingStates.Length; i++)
            {
                CardPresentationState incoming = incomingStates[i];

                incomingRuntimeIDs.Add(incoming.RuntimeID);

                if (existingCards.TryGetValue(incoming.RuntimeID, out Entity existingEntity))
                {
                    EntityManager.SetComponentData(existingEntity, incoming);

                    continue;
                }

                Entity entity = EntityManager.CreateEntity();

                EntityManager.SetName(entity, $"Card Presentation State {incoming.RuntimeID}");

                EntityManager.AddComponentData(entity, incoming);
            }

            foreach (KeyValuePair<uint, Entity> existing in existingCards)
            {
                if (incomingRuntimeIDs.Contains(existing.Key))
                {
                    continue;
                }

                if (EntityManager.Exists(existing.Value))
                {
                    EntityManager.DestroyEntity(existing.Value);
                }
            }
        }

        private void ApplyHandStates(HandPresentationState[] incomingStates)
        {
            Dictionary<BattleSide, Entity> existingHands = new();

            using (NativeArray<Entity> entities = handStateQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];

                    HandPresentationState existing =
                        EntityManager.GetComponentData<HandPresentationState>(entity);

                    existingHands.TryAdd(existing.Side, entity);
                }
            }

            HashSet<BattleSide> incomingSides = new();

            for (int i = 0; i < incomingStates.Length; i++)
            {
                HandPresentationState incoming = incomingStates[i];

                incomingSides.Add(incoming.Side);

                if (existingHands.TryGetValue(incoming.Side, out Entity existingEntity))
                {
                    EntityManager.SetComponentData(existingEntity, incoming);

                    continue;
                }

                Entity entity = EntityManager.CreateEntity();

                EntityManager.SetName(entity, $"Hand Presentation State {incoming.Side}");

                EntityManager.AddComponentData(entity, incoming);
            }

            foreach (KeyValuePair<BattleSide, Entity> existing in existingHands)
            {
                if (incomingSides.Contains(existing.Key))
                {
                    continue;
                }

                if (EntityManager.Exists(existing.Value))
                {
                    EntityManager.DestroyEntity(existing.Value);
                }
            }
        }

        private bool ValidateCardSnapshot(CardPresentationState[] cards)
        {
            HashSet<uint> runtimeIDs = new();

            for (int i = 0; i < cards.Length; i++)
            {
                if (!runtimeIDs.Add(cards[i].RuntimeID))
                {
                    Logging.Error(
                        LogCategory.Presentation,
                        $"[STATE SNAPSHOT IMPORT] Snapshot contains "
                            + $"duplicate Card RuntimeID="
                            + $"{cards[i].RuntimeID}."
                    );

                    return false;
                }
            }

            return true;
        }

        private bool ValidatePlayerSnapshot(PlayerPresentationState[] players)
        {
            HashSet<BattleSide> sides = new();

            for (int i = 0; i < players.Length; i++)
            {
                if (!sides.Add(players[i].Side))
                {
                    Logging.Error(
                        LogCategory.Presentation,
                        $"[STATE SNAPSHOT IMPORT] Snapshot contains "
                            + $"duplicate Player Side="
                            + $"{players[i].Side}."
                    );

                    return false;
                }
            }

            return true;
        }

        private bool ValidateHandSnapshot(HandPresentationState[] hands)
        {
            HashSet<BattleSide> sides = new();

            for (int i = 0; i < hands.Length; i++)
            {
                if (!sides.Add(hands[i].Side))
                {
                    Logging.Error(
                        LogCategory.Presentation,
                        $"[STATE SNAPSHOT IMPORT] Snapshot contains "
                            + $"duplicate Hand Side="
                            + $"{hands[i].Side}."
                    );

                    return false;
                }
            }

            return true;
        }

        private bool ValidateCharacterSnapshot(CharacterPresentationState[] characters)
        {
            HashSet<uint> runtimeIDs = new();

            for (int i = 0; i < characters.Length; i++)
            {
                uint runtimeID = characters[i].RuntimeID;

                if (runtimeIDs.Add(runtimeID))
                {
                    continue;
                }

                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE SNAPSHOT IMPORT] Snapshot contains "
                        + $"duplicate Character RuntimeID="
                        + $"{runtimeID}."
                );

                return false;
            }

            for (int i = 0; i < characters.Length; i++)
            {
                CharacterPresentationState character = characters[i];

                Logging.Info(
                    LogCategory.Presentation,
                    $"[STATE IMPORT CHARACTER] "
                        + $"RuntimeID={character.RuntimeID} | "
                        + $"HP={character.CurrentHealth}/"
                        + $"{character.MaxHealth} | "
                        + $"Alive={character.IsAlive} | "
                );
            }

            return true;
        }

        private void ClearStateMirror()
        {
            using (
                NativeArray<Entity> battleEntities = battleStateQuery.ToEntityArray(Allocator.Temp)
            )
            {
                for (int i = 0; i < battleEntities.Length; i++)
                {
                    EntityManager.DestroyEntity(battleEntities[i]);
                }
            }

            using (
                NativeArray<Entity> characterEntities = characterStateQuery.ToEntityArray(
                    Allocator.Temp
                )
            )
            {
                for (int i = 0; i < characterEntities.Length; i++)
                {
                    EntityManager.DestroyEntity(characterEntities[i]);
                }
            }

            using (
                NativeArray<Entity> playerEntities = playerStateQuery.ToEntityArray(Allocator.Temp)
            )
            {
                for (int i = 0; i < playerEntities.Length; i++)
                {
                    EntityManager.DestroyEntity(playerEntities[i]);
                }
            }

            using (NativeArray<Entity> cardEntities = cardStateQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < cardEntities.Length; i++)
                {
                    EntityManager.DestroyEntity(cardEntities[i]);
                }
            }

            using (NativeArray<Entity> handEntities = handStateQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < handEntities.Length; i++)
                {
                    EntityManager.DestroyEntity(handEntities[i]);
                }
            }

            using (
                NativeArray<Entity> revisionEntities = mirrorRevisionQuery.ToEntityArray(
                    Allocator.Temp
                )
            )
            {
                for (int i = 0; i < revisionEntities.Length; i++)
                {
                    EntityManager.DestroyEntity(revisionEntities[i]);
                }
            }
        }
    }
}
