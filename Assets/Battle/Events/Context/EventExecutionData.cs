namespace Archeus.Battle.Events.Context
{
    public struct EventExecutionData
    {
        public const uint InvalidOperationID = 0;

        public uint OperationID;

        public bool HasOperation => OperationID != InvalidOperationID;
    }
}
