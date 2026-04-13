using System;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Archeus.Game.Bootstrap
{
    public sealed class SeedService : ISeedService
    {
        private readonly Dictionary<string, ulong> derived = new();
        private readonly ulong worldSeed;

        public SeedService()
        {
            worldSeed = GenerateWorldSeed();
        }

        public ulong WorldSeed => worldSeed;

        public ulong CreateDerivedSeed(string key, ulong salt = 0)
        {
            if (derived.TryGetValue(key, out ulong existing))
                return existing;

            ulong keyHash = HashStringToUlong(key);
            ulong mixed = Mix64(worldSeed, keyHash, salt);

            derived[key] = mixed;
            return mixed;
        }

        public DeterministicRNG CreateRNG(string key, ulong salt = 0)
            => new DeterministicRNG(CreateDerivedSeed(key, salt));

        public SeedSnapshot CreateSnapshot()
            => new SeedSnapshot(worldSeed, new Dictionary<string, ulong>(derived));

        public void RestoreSnapshot(in SeedSnapshot snapshot)
        {
            derived.Clear();
            foreach (var kvp in snapshot.Derived)
                derived[kvp.Key] = kvp.Value;
        }

        private static ulong GenerateWorldSeed()
        {
            ulong timeComponent = (ulong)DateTime.UtcNow.Ticks;

            Guid guid = Guid.NewGuid();
            byte[] guidBytes = guid.ToByteArray();
            ulong guidHash = BinaryPrimitives.ReadUInt64LittleEndian(guidBytes);

            ulong combined = timeComponent ^ guidHash;

            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(BitConverter.GetBytes(combined));
            ulong seed = BinaryPrimitives.ReadUInt64LittleEndian(hash);

            return seed;
        }

        private static ulong Mix64(ulong a, ulong b, ulong c)
        {
            a += 0x9E3779B97F4A7C15UL;
            b ^= a;
            b *= 0xBF58476D1CE4E5B9UL;
            c ^= b;
            c *= 0x94D049BB133111EBUL;
            c ^= (c >> 33);
            return c;
        }

        private static ulong HashStringToUlong(string s)
        {
            const ulong fnvOffset = 14695981039346656037UL;
            const ulong fnvPrime = 1099511628211UL;
            ulong hash = fnvOffset;
            foreach (byte b in System.Text.Encoding.UTF8.GetBytes(s))
            {
                hash ^= b;
                hash *= fnvPrime;
            }
            return hash;
        }
    }
}
