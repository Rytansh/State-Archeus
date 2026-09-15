namespace Archeus.Battle.Presentation.Facts
{
    public struct PresentationFactMetadata
    {
        public const uint NoAction = 0;
        public const ushort NoActionResult = ushort.MaxValue;
        public const ushort NoHit = ushort.MaxValue;
        public ushort HitIndex;

        public ulong BattleRuntimeID;

        public uint SourceRuntimeID;
        public uint TargetRuntimeID;

        public ulong Sequence;

        public uint ActionDefinitionID;
        public uint ActionExecutionID;
        public ushort ActionResultIndex;

        public uint GroupID;
        public ushort Generation;
    }

    public struct PresentationFactContext
    {
        public ulong BattleRuntimeID;
        public uint SourceRuntimeID;
        public uint TargetRuntimeID;
        public uint ActionDefinitionID;
        public uint ActionExecutionID;
        public ushort ActionResultIndex;
        public ushort HitIndex;
        public uint GroupID;
        public ushort Generation;
    }
}
