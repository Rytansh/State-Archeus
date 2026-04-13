namespace Archeus.Game.Bootstrap
{
    public interface ISeedService
    {
        ulong WorldSeed { get; }
        ulong CreateDerivedSeed(string key, ulong salt = 0);
        DeterministicRNG CreateRNG(string key, ulong salt = 0);

        SeedSnapshot CreateSnapshot();
        void RestoreSnapshot(in SeedSnapshot snapshot);
    }
}

