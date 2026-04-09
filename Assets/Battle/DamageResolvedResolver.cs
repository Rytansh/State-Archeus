using Unity.Entities;
using System;
using DBUS.Battle.Components.Combat;

public static class DamageResolvedResolver
{
    public static void Resolve(ref BattleSimulationContext ctx, BattleEvent evt)
    {
        var target = evt.Target;
        if (!ctx.HealthLookup.HasComponent(target)) return;

        var hp = ctx.HealthLookup[target];
        float oldHp = hp.Value;
        float damageTaken = evt.Payload.Damage.FinalDamage;

        // Apply the damage
        hp.Value -= damageTaken;
        ctx.HealthLookup[target] = hp;

        // Get MaxHP for percentage tracking (assuming you have a MaxHP component or stat)
        // If MaxHP is in stats:
        var stats = ctx.StatsLookup[target];
        float hpPercent = hp.Value / stats.MaxHealth * 100f;

        Logging.System($"[Health Update] Entity {target.Index}: " + $"{oldHp} -> {hp.Value} (-{damageTaken}) | " +$"Current HP: {hpPercent:F1}%");
        
        if (hpPercent <= 50f)
        {
            Logging.System($"[Threshold reached] Entity {target.Index} is below 50% HP. Passives should now trigger.");
        }
    }
}