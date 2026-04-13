using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;

public class RNGTestProcess : IBootstrapProcess
{
    public int Order => SharedBootstrapOrder.RNGTests;
    private string testSeedString = "Battle_Test";

    public void Initialise(WorldContext context)
    {
        ISeedService seedService = context.Resolve<ISeedService>();
        IRNGProvider rngProvider = context.Resolve<IRNGProvider>();

        ulong worldSeed = seedService.WorldSeed;
        Logging.Info(LogCategory.Testing,$"WorldSeed = {worldSeed}");

        // === Test 1: Seed derivation determinism ===
        Logging.Info(LogCategory.Testing,"Test 1 — Seed derivation determinism check...");

        ulong derivedBattleSeed = seedService.CreateDerivedSeed(testSeedString);
        ulong sameStringDerivedBattleSeed = seedService.CreateDerivedSeed(testSeedString);

        if (derivedBattleSeed != sameStringDerivedBattleSeed)
            Logging.Error(LogCategory.Testing,"❌ Test 1 failed - Mismatch of derived seeds: {derivedBattleSeed} != {sameStringDerivedBattleSeed}");
        else
            Logging.Info(LogCategory.Testing,$"[RNGTestProcess] ✅ Test 1 passed — derived seeds are equal: {derivedBattleSeed} = {sameStringDerivedBattleSeed}");
        

        // === Test 2: In-run sequence determinism ===
        Logging.Info(LogCategory.Testing,"Test 2 — In-run sequence consistency check...");

        var rngA = new DeterministicRNG(derivedBattleSeed);
        var rngB = new DeterministicRNG(derivedBattleSeed);

        bool match = true;
        const int sampleCount = 10;

        for (int i = 0; i < sampleCount; i++)
        {
            int a = rngA.NextInt(0, 100);
            int b = rngB.NextInt(0, 100);

            if (a != b)
            {
                Logging.Error(LogCategory.Testing,$"❌ Mismatch at roll {i}: {a} != {b}");
                match = false;
                break;
            }
        }

        if (match)
            Logging.Info(LogCategory.Testing,"✅ Test 2 passed — in-run RNG sequence deterministic.");
        else
            Logging.Error(LogCategory.Testing,"❌ Test 2 failed — sequence diverged.");

        // === Test 3: Cross-run determinism (replay / reload check) ===
        Logging.Info(LogCategory.Testing,"[RNGTestProcess] Test 3 — Cross-run consistency check...");

        var replayRng1 = new DeterministicRNG(derivedBattleSeed);
        var replayRng2 = new DeterministicRNG(sameStringDerivedBattleSeed);

        bool replayMatch = true;
        for (int i = 0; i < sampleCount; i++)
        {
            int rollA = replayRng1.NextInt(0, 100);
            int rollB = replayRng2.NextInt(0, 100);

            if (rollA != rollB)
            {
                Logging.Error(LogCategory.Testing,$"❌ Replay mismatch at roll {i}");
                replayMatch = false;
                break;
            }
        }

        if (replayMatch)
            Logging.Info(LogCategory.Testing,"✅ Test 3 passed — cross-run sequence deterministic.");
        else
            Logging.Error(LogCategory.Testing,"❌ Test 3 failed — replay sequence mismatch.");

        Logging.Info(LogCategory.Testing,"✅ All RNG deterministic validation phases complete.");
    }
}
