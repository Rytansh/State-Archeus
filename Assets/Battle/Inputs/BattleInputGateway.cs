using Archeus.Battle.Buffers.Input;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
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
                        command.StageRevision = entityManager
                            .GetComponentData<StageSnapshotStatus>(battle)
                            .Revision;

            DynamicBuffer<BattleInputCommand> buffer = entityManager.GetBuffer<BattleInputCommand>(
                inbox
            );

            buffer.Add(command);
        }
    }
}
