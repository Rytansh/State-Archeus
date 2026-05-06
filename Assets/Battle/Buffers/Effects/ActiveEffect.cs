using Unity.Entities;

namespace Archeus.Battle.Buffers.Events
{
    public struct ActiveEffect : IBufferElementData
    {
        public int EffectIndex;
        public float Strength;
        public int RemainingDuration;
        public int Stacks;
    }
}

