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
        SelectTarget,

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
        CheckTargetHP,

        Jump,
        JumpIfFalse,
        JumpIfTrue,
        End,
    }
}
