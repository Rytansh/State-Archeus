using Archeus.Battle.Presentation.State;
using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup), OrderFirst = true)]
    [UpdateAfter(typeof(PresentationSnapshotImportSystem))]
    public partial class PresentationUpdateImportSystem : SystemBase
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

            cardStateQuery = GetEntityQuery(ComponentType.ReadWrite<CardPresentationState>());

            handStateQuery = GetEntityQuery(ComponentType.ReadWrite<HandPresentationState>());

            playerStateQuery = GetEntityQuery(ComponentType.ReadWrite<PlayerPresentationState>());

            characterStateQuery = GetEntityQuery(
                ComponentType.ReadWrite<CharacterPresentationState>()
            );
            mirrorRevisionQuery = GetEntityQuery(
                ComponentType.ReadWrite<PresentationMirrorRevision>()
            );

            RequireForUpdate(bridgeQuery);
        }

        protected override void OnUpdate()
        {
            Entity bridgeEntity = bridgeQuery.GetSingletonEntity();

            BattlePresentationBridgeReference bridgeReference =
                EntityManager.GetComponentObject<BattlePresentationBridgeReference>(bridgeEntity);

            BattlePresentationBridge bridge = bridgeReference.Bridge;

            while (bridge.TryConsumeStateUpdate(out PresentationStateUpdate update))
            {
                if (update == null)
                {
                    Logging.Error(
                        LogCategory.Presentation,
                        "[STATE UPDATE IMPORT] Received null state update."
                    );

                    continue;
                }

                if (
                    !TryValidateRevision(
                        bridge,
                        update,
                        out Entity revisionEntity,
                        out PresentationMirrorRevision mirrorRevision
                    )
                )
                {
                    continue;
                }

                bool applied = TryApplyUpdate(update);

                if (!applied)
                {
                    continue;
                }

                mirrorRevision.Value = update.Revision;

                EntityManager.SetComponentData(revisionEntity, mirrorRevision);
            }
        }

        private bool TryApplyUpdate(PresentationStateUpdate update)
        {
            switch (update)
            {
                case BattlePresentationStateUpdate battleUpdate:
                    return TryApplyBattleUpdate(battleUpdate);

                case CharacterPresentationStateUpdate characterUpdate:
                    return TryApplyCharacterUpdate(characterUpdate);

                case CardPresentationStateUpdate cardUpdate:
                    return TryApplyCardUpdate(cardUpdate);

                case HandPresentationStateUpdate handUpdate:
                    return TryApplyHandUpdate(handUpdate);

                case PlayerPresentationStateUpdate playerUpdate:
                    return TryApplyPlayerUpdate(playerUpdate);

                default:
                {
                    Logging.Error(
                        LogCategory.Presentation,
                        $"[STATE UPDATE IMPORT] "
                            + $"Unsupported update type "
                            + $"'{update.GetType().Name}'."
                    );

                    return false;
                }
            }
        }

        private bool TryApplyBattleUpdate(BattlePresentationStateUpdate update)
        {
            if (!TryGetCurrentBattleState(out BattlePresentationState current))
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE UPDATE IMPORT] "
                        + $"Received Battle update before "
                        + $"a snapshot created the mirror. "
                        + $"Battle={update.BattleRuntimeID} | "
                        + $"Revision={update.Revision}"
                );

                return false;
            }

            if (current.BattleRuntimeID != update.BattleRuntimeID)
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE UPDATE IMPORT] Battle mismatch | "
                        + $"MirrorBattle={current.BattleRuntimeID} | "
                        + $"UpdateBattle={update.BattleRuntimeID} | "
                        + $"Revision={update.Revision}"
                );

                return false;
            }

            Entity battleEntity = battleStateQuery.GetSingletonEntity();

            EntityManager.SetComponentData(battleEntity, update.State);

            Logging.Info(
                LogCategory.Presentation,
                $"[STATE UPDATE IMPORT] "
                    + $"Battle={update.BattleRuntimeID} | "
                    + $"Revision={update.Revision} | "
                    + $"Phase={update.State.Phase} | "
                    + $"Turn={update.State.TurnNumber} | "
            );
            return true;
        }

        private bool TryApplyPlayerUpdate(PlayerPresentationStateUpdate update)
        {
            using NativeArray<Entity> entities = playerStateQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];

                PlayerPresentationState existing =
                    EntityManager.GetComponentData<PlayerPresentationState>(entity);

                if (existing.Side != update.State.Side)
                {
                    continue;
                }

                EntityManager.SetComponentData(entity, update.State);

                Logging.Info(
                    LogCategory.Presentation,
                    $"[PLAYER STATE UPDATE IMPORT] "
                        + $"Battle={update.BattleRuntimeID} | "
                        + $"Revision={update.Revision} | "
                        + $"RemainingAP={update.State.ActionPoints}"
                );

                return true;
            }

            Entity playerEntity = EntityManager.CreateEntity();

            EntityManager.SetName(playerEntity, $"Player Presentation State {update.State.Side}");

            EntityManager.AddComponentData(playerEntity, update.State);

            return true;
        }

        private bool TryApplyCharacterUpdate(CharacterPresentationStateUpdate update)
        {
            if (!TryGetCurrentBattleState(out BattlePresentationState currentBattle))
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[CHARACTER STATE UPDATE IMPORT] "
                        + "Received character update before "
                        + "a snapshot created the battle mirror."
                );

                return false;
            }

            if (currentBattle.BattleRuntimeID != update.BattleRuntimeID)
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[CHARACTER STATE UPDATE IMPORT] "
                        + $"Battle mismatch | "
                        + $"MirrorBattle="
                        + $"{currentBattle.BattleRuntimeID} | "
                        + $"UpdateBattle="
                        + $"{update.BattleRuntimeID} | "
                        + $"Revision={update.Revision}"
                );

                return false;
            }

            using NativeArray<Entity> entities = characterStateQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];

                CharacterPresentationState current =
                    EntityManager.GetComponentData<CharacterPresentationState>(entity);

                if (current.RuntimeID != update.State.RuntimeID)
                {
                    continue;
                }

                EntityManager.SetComponentData(entity, update.State);

                Logging.Info(
                    LogCategory.Presentation,
                    $"[CHARACTER STATE UPDATE IMPORT] "
                        + $"Battle={update.BattleRuntimeID} | "
                        + $"Revision={update.Revision} | "
                        + $"Character="
                        + $"{update.State.RuntimeID} | "
                        + $"HP={update.State.CurrentHealth}/"
                        + $"{update.State.MaxHealth} | "
                        + $"Alive={update.State.IsAlive}"
                );

                return true;
            }

            /*
             * A character can legitimately be introduced
             * after the initial full snapshot.
             *
             * Treat a missing mirror as a new projected
             * character rather than an automatic failure.
             */
            Entity characterEntity = EntityManager.CreateEntity();

            EntityManager.SetName(
                characterEntity,
                $"Character Presentation State " + $"{update.State.RuntimeID}"
            );

            EntityManager.AddComponentData(characterEntity, update.State);

            EntityManager.AddComponentData(
                characterEntity,
                new CharacterVisibleState
                {
                    Health = update.State.CurrentHealth,
                    IsAlive = update.State.IsAlive,
                }
            );

            Logging.Info(
                LogCategory.Presentation,
                $"[CHARACTER STATE UPDATE IMPORT] "
                    + $"Created mirror character | "
                    + $"Battle={update.BattleRuntimeID} | "
                    + $"Revision={update.Revision} | "
                    + $"Character={update.State.RuntimeID}"
            );

            return true;
        }

        private bool TryApplyCardUpdate(CardPresentationStateUpdate update)
        {
            using NativeArray<Entity> entities = cardStateQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];

                CardPresentationState existing =
                    EntityManager.GetComponentData<CardPresentationState>(entity);

                if (existing.RuntimeID != update.State.RuntimeID)
                    continue;

                EntityManager.SetComponentData(entity, update.State);

                Logging.Info(
                    LogCategory.Presentation,
                    $"[CARD STATE UPDATE IMPORT] "
                        + $"Battle={update.BattleRuntimeID} | "
                        + $"Revision={update.Revision} | "
                        + $"Card={update.State.RuntimeID} | "
                        + $"Zone={update.State.Location.Zone}"
                );

                return true;
            }

            Entity cardEntity = EntityManager.CreateEntity();

            EntityManager.SetName(cardEntity, $"Card Presentation State {update.State.RuntimeID}");

            EntityManager.AddComponentData(cardEntity, update.State);

            return true;
        }

        private bool TryApplyHandUpdate(HandPresentationStateUpdate update)
        {
            using NativeArray<Entity> entities = handStateQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];

                HandPresentationState existing =
                    EntityManager.GetComponentData<HandPresentationState>(entity);

                if (existing.Side != update.State.Side)
                    continue;

                EntityManager.SetComponentData(entity, update.State);

                Logging.Info(
                    LogCategory.Presentation,
                    $"[HAND STATE UPDATE IMPORT] "
                        + $"Battle={update.BattleRuntimeID} | "
                        + $"Revision={update.Revision} | "
                        + $"MaxSize={update.State.MaxSize}"
                );

                return true;
            }

            Entity handEntity = EntityManager.CreateEntity();

            EntityManager.SetName(handEntity, $"Hand Presentation State {update.State.Side}");

            EntityManager.AddComponentData(handEntity, update.State);

            return true;
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
                    $"[STATE UPDATE IMPORT] "
                        + $"PresentationWorld contains "
                        + $"{entities.Length} "
                        + $"BattlePresentationState entities. "
                        + "Expected exactly one."
                );

                battleState = default;

                return false;
            }

            battleState = EntityManager.GetComponentData<BattlePresentationState>(entities[0]);

            return true;
        }

        private bool TryValidateRevision(
            BattlePresentationBridge bridge,
            PresentationStateUpdate update,
            out Entity revisionEntity,
            out PresentationMirrorRevision mirrorRevision
        )
        {
            using NativeArray<Entity> entities = mirrorRevisionQuery.ToEntityArray(Allocator.Temp);

            if (entities.Length != 1)
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE UPDATE IMPORT] Expected exactly one "
                        + $"PresentationMirrorRevision, found "
                        + $"{entities.Length}."
                );

                revisionEntity = Entity.Null;
                mirrorRevision = default;

                return false;
            }

            revisionEntity = entities[0];

            mirrorRevision = EntityManager.GetComponentData<PresentationMirrorRevision>(
                revisionEntity
            );

            if (mirrorRevision.BattleRuntimeID != update.BattleRuntimeID)
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE UPDATE IMPORT] Revision battle mismatch | "
                        + $"MirrorBattle={mirrorRevision.BattleRuntimeID} | "
                        + $"UpdateBattle={update.BattleRuntimeID}"
                );

                return false;
            }
            if (mirrorRevision.Status != PresentationMirrorSyncStatus.Synchronized)
            {
                /*
                 * A recovery snapshot will supersede these updates.
                 *
                 * Continue draining the queue, but do not apply them.
                 */
                return false;
            }

            ulong expectedRevision = mirrorRevision.Value + 1;

            if (update.Revision < expectedRevision)
            {
                Logging.Warn(
                    LogCategory.Presentation,
                    $"[STATE UPDATE IMPORT] Ignoring stale update | "
                        + $"Incoming={update.Revision} | "
                        + $"Current={mirrorRevision.Value}"
                );

                return false;
            }

            if (update.Revision > expectedRevision)
            {
                Logging.Error(
                    LogCategory.Presentation,
                    $"[STATE UPDATE IMPORT] "
                        + $"Revision gap detected | "
                        + $"Battle={update.BattleRuntimeID} | "
                        + $"Expected={expectedRevision} | "
                        + $"Received={update.Revision}"
                );

                mirrorRevision.Status = PresentationMirrorSyncStatus.Recovering;

                EntityManager.SetComponentData(revisionEntity, mirrorRevision);

                bridge.RequestStateRecovery(update.BattleRuntimeID);

                return false;
            }

            return true;
        }
    }
}
