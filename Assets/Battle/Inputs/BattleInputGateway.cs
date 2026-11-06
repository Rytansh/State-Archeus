using Archeus.Battle.Buffers.Input;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Scheduling;
using Archeus.Battle.Snapshots;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Game.Input
{
    public sealed class BattleInputGateway
    {
        private readonly EntityManager entityManager;
        private readonly Entity inbox;

        private ulong nextSequence = 1;

        public BattleInputGateway(EntityManager entityManager, Entity inbox)
        {
            this.entityManager = entityManager;
            this.inbox = inbox;
        }

        public void RequestDrawCards(ulong battleID, BattleSide side)
        {
            Enqueue(
                new BattleInputCommand
                {
                    BattleID = battleID,
                    Side = side,
                    Type = BattleInputCommandType.DrawCards,
                }
            );
        }

        public void RequestPlaceCard(
            ulong battleID,
            BattleSide side,
            uint cardRuntimeID,
            FieldPosition position
        )
        {
            Enqueue(
                new BattleInputCommand
                {
                    BattleID = battleID,
                    Side = side,
                    Type = BattleInputCommandType.PlaceCard,
                    CardRuntimeID = cardRuntimeID,
                    FieldPosition = position,
                }
            );
        }

        public void RequestRetreatCard(ulong battleID, BattleSide side, uint cardRuntimeID)
        {
            Enqueue(
                new BattleInputCommand
                {
                    BattleID = battleID,
                    Side = side,
                    Type = BattleInputCommandType.RetreatCard,
                    CardRuntimeID = cardRuntimeID,
                }
            );
        }

        public void RequestEndDrawing(ulong battleID, BattleSide side)
        {
            Enqueue(
                new BattleInputCommand
                {
                    BattleID = battleID,
                    Side = side,
                    Type = BattleInputCommandType.EndDrawing,
                }
            );
        }

        public void RequestEndPlanning(ulong battleID, BattleSide side) =>
            Enqueue(
                new BattleInputCommand
                {
                    BattleID = battleID,
                    Side = side,
                    Type = BattleInputCommandType.EndPlanning,
                }
            );

        public void RequestRevertStage(ulong battleID, BattleSide side) =>
            Enqueue(
                new BattleInputCommand
                {
                    BattleID = battleID,
                    Side = side,
                    Type = BattleInputCommandType.RevertStage,
                }
            );

        private void Enqueue(BattleInputCommand command)
        {
            if (
                inbox == Entity.Null
                || !entityManager.Exists(inbox)
                || !entityManager.HasBuffer<BattleInputCommand>(inbox)
            )
            {
                throw new System.InvalidOperationException("Battle input inbox is unavailable.");
            }

            command.Sequence = nextSequence++;

            // Capture the current input fence at enqueue time. A delayed command from an abandoned
            // stage/revert generation must not mutate the freshly restored battle.
            using (
                var query = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<BattleTag>(),
                    ComponentType.ReadOnly<BattleID>(),
                    ComponentType.ReadOnly<StageSnapshotStatus>()
                )
            )
            using (NativeArray<Entity> battles = query.ToEntityArray(Allocator.Temp))
                foreach (Entity battle in battles)
                    if (entityManager.GetComponentData<BattleID>(battle).Value == command.BattleID)
                    {
                        command.StageRevision = entityManager
                            .GetComponentData<StageSnapshotStatus>(battle)
                            .Revision;
                        if (
                            ActionQueueService.TryActive(
                                entityManager,
                                battle,
                                out _,
                                out var active
                            )
                        )
                            command.ContextID = active.ContextID;
                        if (entityManager.HasComponent<BattlePhaseEpoch>(battle))
                            command.PhaseEpoch = entityManager
                                .GetComponentData<BattlePhaseEpoch>(battle)
                                .Value;
                    }

            DynamicBuffer<BattleInputCommand> buffer = entityManager.GetBuffer<BattleInputCommand>(
                inbox
            );

            buffer.Add(command);
        }

        public void RequestQueueAction(
            ulong battleID,
            BattleSide side,
            uint source,
            uint target,
            CharacterActionType type = CharacterActionType.NormalAttack,
            uint definitionID = 0
        ) =>
            Enqueue(
                new BattleInputCommand
                {
                    BattleID = battleID,
                    Side = side,
                    Type = BattleInputCommandType.QueueAction,
                    CardRuntimeID = source,
                    TargetRuntimeID = target,
                    ActionType = type,
                    DefinitionID = definitionID,
                }
            );

        public void RequestRemoveAction(ulong battleID, BattleSide side, uint queueID) =>
            Enqueue(
                new BattleInputCommand
                {
                    BattleID = battleID,
                    Side = side,
                    Type = BattleInputCommandType.RemoveAction,
                    QueueActionID = queueID,
                }
            );

        public void RequestMoveAction(
            ulong battleID,
            BattleSide side,
            uint queueID,
            int position
        ) =>
            Enqueue(
                new BattleInputCommand
                {
                    BattleID = battleID,
                    Side = side,
                    Type = BattleInputCommandType.MoveAction,
                    QueueActionID = queueID,
                    Position = position,
                }
            );

        public void RequestSelectInterrupt(
            ulong battleID,
            BattleSide side,
            uint selectionID,
            uint queueID
        ) =>
            Enqueue(
                new BattleInputCommand
                {
                    BattleID = battleID,
                    Side = side,
                    Type = BattleInputCommandType.SelectInterrupt,
                    SelectionID = selectionID,
                    QueueActionID = queueID,
                }
            );

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void EnableSchedulerDebug(ulong battleID, BattleSide side)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BattlePlayerIdentity>(),
                ComponentType.ReadOnly<OwnedBattle>(),
                ComponentType.ReadOnly<BattleSideAssignment>()
            );
            using NativeArray<Entity> players = query.ToEntityArray(Allocator.Temp);
            foreach (Entity player in players)
            {
                Entity battle = entityManager.GetComponentData<OwnedBattle>(player).Battle;
                if (
                    entityManager.GetComponentData<BattleSideAssignment>(player).Side == side
                    && entityManager.Exists(battle)
                    && entityManager.GetComponentData<BattleID>(battle).Value == battleID
                    && !entityManager.HasComponent<Archeus.Battle.Input.SchedulerDebugAuthority>(
                        player
                    )
                )
                    entityManager.AddComponent<Archeus.Battle.Input.SchedulerDebugAuthority>(
                        player
                    );
            }
        }
#endif

        public void RequestDebugSchedulerControl(
            ulong battleID,
            BattleSide side,
            ActionControlKind kind,
            uint actor,
            uint target,
            CharacterActionType type = CharacterActionType.NormalAttack,
            uint queueID = 0,
            int position = 0
        ) =>
            Enqueue(
                new BattleInputCommand
                {
                    BattleID = battleID,
                    Side = side,
                    Type = BattleInputCommandType.DebugSchedulerControl,
                    ControlKind = kind,
                    CardRuntimeID = actor,
                    TargetRuntimeID = target,
                    ActionType = type,
                    QueueActionID = queueID,
                    Position = position,
                }
            );
    }
}
