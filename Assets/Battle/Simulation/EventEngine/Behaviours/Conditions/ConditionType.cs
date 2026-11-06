namespace Archeus.Battle.Data.Events
{
    public enum ConditionType
    {
        HPBelowPercent,
        HPBelowFlat,
        HPAbovePercent,
        HPAboveFlat,
        DamageAbove,
        DamageBelow,
        RuntimeIDEquals,
        ActionTypeEquals,
    }

    public enum ConditionTarget
    {
        Self,
        Target,
        Source,
    }

    public enum TriggerOwnerType
    {
        Any,
        Source,
        Target,
    }
}
