using System;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Events.Context;
using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;

public static class BattleRNGService
{
    public static bool RollChance(ref BattleRNG battleRNG, float chance)
    {
        DeterministicRNG rng = new DeterministicRNG(battleRNG.StateA, battleRNG.StateB);

        Logging.Info(LogCategory.RNG, $"Performing roll with a {chance}% chance of success...");

        chance /= 100f;
        chance = Math.Clamp(chance, 0f, 1f);

        bool result = rng.NextFloat() < chance;

        if (result)
        {
            Logging.Info(LogCategory.RNG, $"Roll succeeded.");
        }
        else
        {
            Logging.Info(LogCategory.RNG, $"Roll failed.");
        }

        battleRNG.StateA = rng.StateA;
        battleRNG.StateB = rng.StateB;

        return result;
    }

    public static int RollInt(ref BattleRNG battleRNG, int min, int max)
    {
        DeterministicRNG rng = new DeterministicRNG(battleRNG.StateA, battleRNG.StateB);

        Logging.Info(LogCategory.RNG, $"Rolling an int between {min} and {max}...");

        int result = rng.NextInt(min, max);

        Logging.Info(LogCategory.RNG, $"Rolled {result}.");

        battleRNG.StateA = rng.StateA;
        battleRNG.StateB = rng.StateB;

        return result;
    }
}
