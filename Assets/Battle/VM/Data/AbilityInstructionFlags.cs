using System;

namespace Archeus.Battle.Data.VM
{
    [Flags]
    public enum AbilityInstructionFlags : byte
    {
        None = 0,

        GameplayOperation = 1 << 0,

        StartsActionHit = 1 << 1,
    }
}
