namespace Archeus.Battle.Data.VM
{
    public enum AbilityOpcode : byte
    {
        PushConst,
        PushStat,
        PushEventValue,
        LoadState,
        
        StoreState,
        ModifyEventValue,

        Add,
        Sub,
        Mul,
        Div,

        Equal,
        Greater,
        GreaterEqual,
        Less,
        LessEqual,

        DealDamage,
        ApplyEffect,

        Jump,
        JumpIfFalse,
        JumpIfTrue,
        End
    }
}

