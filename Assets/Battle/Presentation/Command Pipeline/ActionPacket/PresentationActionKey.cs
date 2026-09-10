using System;

public struct PresentationActionKey : IEquatable<PresentationActionKey>
{
    public ulong BattleRuntimeID;
    public uint ActionExecutionID;

    public bool Equals(PresentationActionKey other)
    {
        return BattleRuntimeID == other.BattleRuntimeID
            && ActionExecutionID == other.ActionExecutionID;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (BattleRuntimeID.GetHashCode() * 397) ^ (int)ActionExecutionID;
        }
    }
}
