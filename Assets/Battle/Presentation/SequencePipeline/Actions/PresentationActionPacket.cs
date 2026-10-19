using Unity.Entities;

namespace Archeus.Battle.Components.Presentation
{
    public struct PresentationActionPacket : IComponentData
    {
        public ulong BattleRuntimeID;
        public uint ActionExecutionID;

        public uint ActionDefinitionID;
        public uint SourceRuntimeID;

        public ulong FirstSequence;
        public ulong LastSequence;

        public PresentationActionPacketStatus Status;
    }
}
