using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Unity.Entities;

namespace Archeus.Battle.Buffers.Input
{
    public enum BattleInputCommandType : byte
    {
        DrawCards,
        PlaceCard,
        RetreatCard,
        EndDrawing,
        EndPlanning,
        RevertStage,
        QueueAction,
        RemoveAction,
        MoveAction,
        SelectInterrupt,
        DebugSchedulerControl,
    }

    public struct BattleInputCommand : IBufferElementData
    {
        public ulong Sequence;
        public ulong BattleID;
        public BattleSide Side;
        public BattleInputCommandType Type;
        public ulong StageRevision;
        public uint ContextID;
        public uint PhaseEpoch;
        public uint TargetRuntimeID;
        public uint QueueActionID;
        public uint SelectionID;
        public int Position;
        public uint DefinitionID;
        public Archeus.Battle.Data.Actions.CharacterActionType ActionType;
        public Archeus.Battle.Scheduling.ActionControlKind ControlKind;

        public uint CardRuntimeID;
        public FieldPosition FieldPosition;
    }
}
