namespace Archeus.Battle.Data.Effects
{
    public struct StatModifier
    {
        public StatType StatType;
        public ModifierOperation ModifierType;
    }

    public enum StatType : byte
    {
        Attack,
        Defense,
        MaxHealth,

        CritRate,
        CritDamage,

        Reactivity,
        SustainPower,
        Authority,
        Stability,
        PoolAffinity,

        ALLDamageBonus,
        DOTDamageBonus
    }

    public enum ModifierOperation : byte
    {
        AddFlat,
        AddPercent,
        MultiplyFinal,
        Override
    }
}
