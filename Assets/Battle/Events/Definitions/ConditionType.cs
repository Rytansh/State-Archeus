namespace Archeus.Battle.Events.Definitions
{
    public enum ConditionType
    {
        HPBelowPercent,
        HPBelowFlat,
        HPAbovePercent,
        HPAboveFlat,
        DamageAbove,
        DamageBelow,
    }
    public enum ConditionTarget
    {
        Self,
        Target,
        Source
    }
}
