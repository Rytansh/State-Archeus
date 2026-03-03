using Unity.Entities;

namespace DBUS.Battle.Components.Effects
{
    public enum ReactiveEffectType
    {
        PowerResidue,
        TacticalImprint,
        FlexibleMark,
        MagicalStain,
        ReactiveBridge
    }

    public struct ReactiveEffectInstance : IBufferElementData
    {
        public ReactiveEffectType Type;
        public int TurnsRemaining;
        public int Stacks;
    }
}