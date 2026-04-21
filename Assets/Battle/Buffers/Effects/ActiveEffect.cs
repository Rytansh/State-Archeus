using Unity.Entities;

namespace Archeus.Buffers.Effects
{
    public struct ActiveEffect : IBufferElementData
    {
        public int EffectIndex;
        public int RemainingDuration;
        public int Stacks;
    }
}
