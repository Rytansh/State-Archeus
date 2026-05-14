using Unity.Entities;

namespace Archeus.Battle.Buffers.Events
{
    public struct ActiveEffect : IBufferElementData
    {
        public int EffectIndex;
        public Entity Source;
        public float Strength;
        public int RemainingDuration;
        public bool IsPermanent;
        public int Stacks;
    }
}

