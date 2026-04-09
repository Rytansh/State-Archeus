
namespace DBUS.Battle.VM.Data
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

        Jump,
        JumpIfFalse,
        JumpIfTrue,
        End
    }
}

