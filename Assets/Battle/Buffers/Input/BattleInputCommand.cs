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
    }

    public struct BattleInputCommand : IBufferElementData
    {
        public ulong Sequence;
        public ulong BattleID;
        public BattleSide Side;
        public BattleInputCommandType Type;

        public uint CardRuntimeID;
        public FieldPosition FieldPosition;
    }
}
