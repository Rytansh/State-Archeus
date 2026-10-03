using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Archeus.Battle.Data.VM;

namespace Archeus.Battle.VM
{
    public static class AbilityOpcodeSemantics
    {
        public static AbilityInstructionFlags GetFlags(AbilityOpcode opcode)
        {
            return opcode switch
            {
                AbilityOpcode.DealDamage => AbilityInstructionFlags.GameplayOperation
                    | AbilityInstructionFlags.StartsActionHit,

                AbilityOpcode.ApplyEffect => AbilityInstructionFlags.GameplayOperation,

                _ => AbilityInstructionFlags.None,
            };
        }
    }
}
