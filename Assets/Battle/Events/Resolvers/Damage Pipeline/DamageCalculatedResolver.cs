using System;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Data.Effects;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Events.Runtime;
using Archeus.Battle.Stats;
using Unity.Entities;

namespace Archeus.Battle.Events.Resolvers
{
    public static class DamageCalculatedResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt)
        {
            Entity attacker = evt.Source;
            Entity target = evt.Target;

            float finalDamage = evt.Payload.Damage.FinalDamage;
            float criticalRate = StatResolver.Resolve(attacker, StatType.CritRate, ref ctx);
            float critMultiplier = 1f;

            //apply multipliers, modifiers, etc all to inflictedDamage.
            finalDamage *= evt.Payload.Damage.AttackMultiplier;

            bool didCrit = BattleRNGService.RollChance(ref ctx, criticalRate);

            if (didCrit)
            {
                float criticalDamage = StatResolver.Resolve(attacker, StatType.CritDamage, ref ctx);
                critMultiplier = 1  +  criticalDamage / 100;
                finalDamage *= critMultiplier;
            }
            
            ctx.ChainBuffer.Add(new ChainedBattleEvent
            {
                Event = new BattleEvent
                {
                    Type = BattleEventType.DamageMitigated,
                    Scope = evt.Scope,
                    Source = attacker,
                    Target = target,
                    Payload = new EventPayload
                    {
                        Damage = new DamagePayload
                        {
                            AttackMultiplier = evt.Payload.Damage.AttackMultiplier,
                            BaseDamage = evt.Payload.Damage.BaseDamage,
                            FinalDamage = finalDamage,
                            DidCrit = didCrit,
                            CritMultiplier = critMultiplier
                        }
                    }
                }
            });
        }
    }
}