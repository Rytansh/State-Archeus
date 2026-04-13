using System.Collections.Generic;

namespace Archeus.Game.Bootstrap
{
    public readonly struct SeedSnapshot
    {
        public readonly ulong RootSeed;
        public readonly IReadOnlyDictionary<string, ulong> Derived;

        public SeedSnapshot(ulong rootSeed, IReadOnlyDictionary<string, ulong> derived)
        {
            RootSeed = rootSeed;
            Derived = derived;
        }
    }
}

