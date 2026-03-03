using Unity.Entities;

namespace DBUS.Battle.Components.Effects
{
    public enum BasicEffectType
    {
        IncreaseAttack,
        IncreaseDefense,
        IncreaseHealth,
        Shielded,
        Immune,
        Immortal,
        Revivable,
        Invisible,
        Rapid,
        Readiness,
        Reflection,
        Taunt,
        PinpointAccuracy,
        Regeneration,
        Efficient,
        Capable,
        DecreaseAttack,
        DecreaseDefense,
        DecreaseHealth,
        Sluggish,
        Vulnerable,
        Weak,
        Extinction,
        Rebellious,
        Unbuffable,
        Disable,
        Blinded,
        Suppressed,
        Spread,
        Ill,
        Limited
    }

    public struct BasicEffectInstance : IBufferElementData
    {
        public BasicEffectType Type;
        public int TurnsRemaining;
        public int Stacks;
        public float Strength;
    }
}