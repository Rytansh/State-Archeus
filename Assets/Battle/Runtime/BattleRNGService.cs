using Archeus.Battle.Components.Core;
using Archeus.Battle.Events.Runtime;
using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;
using System;

public static class BattleRNGService
{
    public static bool RollChance(ref BattleContext ctx, float chance)
    {
        BattleRNG battleRNG = ctx.RNGLookup[ctx.Battle];

        DeterministicRNG rng = new DeterministicRNG(battleRNG.StateA, battleRNG.StateB);

        Logging.Info(LogCategory.RNG, $"Performing roll with {chance} chance of success...");

        chance /= 100f;
        chance = Math.Clamp(chance, 0f, 1f);

        bool result = rng.NextFloat() < chance;

        if (result)
        {
            Logging.Info(LogCategory.RNG, $"Roll succeeded.");
        } else
        {
            Logging.Info(LogCategory.RNG, $"Roll failed.");
        }

        battleRNG.StateA = rng.StateA;
        battleRNG.StateB = rng.StateB;

        ctx.RNGLookup[ctx.Battle] = battleRNG;

        return result;
    }
}
