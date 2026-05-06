namespace Archeus.Battle.VM.Programs
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

