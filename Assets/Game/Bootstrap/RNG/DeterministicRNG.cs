using System;
using System.Runtime.CompilerServices;

namespace Archeus.Game.Bootstrap
{
    public struct DeterministicRNG
    {
        private ulong _s0;
        private ulong _s1;

        public DeterministicRNG(ulong seed)
        {
            // Split the seed into two non-zero states
            _s0 = SplitMix64(ref seed);
            _s1 = SplitMix64(ref seed);
            if (_s0 == 0 && _s1 == 0) _s1 = 0x9E3779B97F4A7C15UL;
        }

        public DeterministicRNG(ulong stateA, ulong stateB)
        {
            _s0 = stateA;
            _s1 = stateB;
            if (_s0 == 0 && _s1 == 0) _s1 = 0x9E3779B97F4A7C15UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SplitMix64(ref ulong seed)
        {
            ulong z = seed += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong NextU64()
        {
            ulong s1 = _s0;
            ulong s0 = _s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return _s1 + s0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt(int min, int max)
        {
            if (min >= max) throw new ArgumentException("min must be less than max");
            uint range = (uint)(max - min);
            ulong rem = NextU64() % (ulong)range;
            return min + (int)rem;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float NextFloat()
        {
            // 24 bits of precision (0.0–1.0)
            return (NextU64() & 0xFFFFFF) / (float)0x1000000;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double NextDouble()
        {
            // 53 bits of precision (0.0–1.0)
            return (NextU64() >> 11) * (1.0 / (1UL << 53));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBool()
        {
            return (NextU64() & 1UL) == 0UL;
        }

        public ulong StateA => _s0;
        public ulong StateB => _s1;
    }
}
