

namespace Archeus.Battle.Events.Context
{
    public struct EventStructuralData
{
    public const uint InvalidGroupID = 0;
    public const uint InvalidParentFrameID = 0;

    public uint GroupID;
    public ushort Generation;
    public uint ParentFrameID;

    public bool HasStructuralData => GroupID != InvalidGroupID;
}
}