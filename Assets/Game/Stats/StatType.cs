namespace Archeus.Game.Stats
{
    public enum StatType
    {
        Attack = 0,
        Defense = 1,
        MaxHealth = 2,
        CritRATE = 3,
        CritDMG = 4,

        Count
    }

    public enum StatModifierType
    {
        Flat,
        PercentAdd,
        PercentMultiply
    }
}
