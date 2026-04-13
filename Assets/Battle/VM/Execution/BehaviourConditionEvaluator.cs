using Unity.Entities;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Events.Runtime;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Components.Stats;
using Archeus.Content.Blobs;
using Archeus.Core.Debugging;

namespace Archeus.Battle.VM.Execution
{
    public static class BehaviourConditionEvaluator
    {
        public static bool Evaluate(Entity conditionOwner, BattleEvent tiedEvent, ref BattleContext ctx, ref BehaviourTriggerBlob trigger)
        {
            if (trigger.Conditions.Length == 0) {return true;}

            Logging.Info(LogCategory.VM, $"Checking {tiedEvent.Type} on {conditionOwner.Index}");
            for (int i = 0; i < trigger.Conditions.Length; i++)
            {
                ref EventConditionBlob condition = ref trigger.Conditions[i];

                bool result = EvaluateSingleCondition(conditionOwner, condition, tiedEvent, ref ctx);
                Logging.Info(LogCategory.VM, $"Condition {condition.Type} → {result}");
                if (!result)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool EvaluateSingleCondition(Entity conditionOwner, EventConditionBlob condition, BattleEvent tiedEvent, ref BattleContext ctx)
        {
            Entity resolvedEntity = ResolveTargetEntity(conditionOwner, condition, tiedEvent);
            if(resolvedEntity == Entity.Null) { return false; }

            return ConditionFulfilled(resolvedEntity, condition, tiedEvent, ref ctx);
        }

        private static bool ConditionFulfilled(Entity targetEntity, EventConditionBlob condition, BattleEvent tiedEvent, ref BattleContext ctx)
        {
            switch (condition.Type)
            {
                case ConditionType.HPBelowPercent:
                {
                    if (!ctx.HealthLookup.HasComponent(targetEntity)) return false;

                    CurrentHealth hp = ctx.HealthLookup[targetEntity];
                    CharacterStats stats = ctx.StatsLookup[targetEntity];

                    float percent = hp.Value / stats.MaxHealth * 100f;
                    return percent < condition.Value;
                }

                case ConditionType.HPAbovePercent:
                {
                    if (!ctx.HealthLookup.HasComponent(targetEntity)) return false;

                    CurrentHealth hp = ctx.HealthLookup[targetEntity];
                    CharacterStats stats = ctx.StatsLookup[targetEntity];

                    float percent = hp.Value / stats.MaxHealth * 100f;
                    return percent > condition.Value;
                }

                case ConditionType.HPBelowFlat:
                {
                    if (!ctx.HealthLookup.HasComponent(targetEntity)) return false;

                    return ctx.HealthLookup[targetEntity].Value < condition.Value;
                }

                case ConditionType.HPAboveFlat:
                {
                    if (!ctx.HealthLookup.HasComponent(targetEntity)) return false;

                    return ctx.HealthLookup[targetEntity].Value > condition.Value;
                }

                case ConditionType.DamageAbove:
                {
                    return tiedEvent.Payload.Damage.BaseDamage > condition.Value;
                }

                case ConditionType.DamageBelow:
                {
                    return tiedEvent.Payload.Damage.BaseDamage < condition.Value;
                }

                default:
                    return true;
            }
        }

        private static Entity ResolveTargetEntity(Entity conditionOwner, EventConditionBlob condition, BattleEvent tiedEvent)
        {
            Entity entity;

            switch (condition.Target)
            {
                case ConditionTarget.Self:
                    entity = conditionOwner;
                    break;

                case ConditionTarget.Target:
                    if (tiedEvent.Scope == BattleEventScope.Global)
                    {
                        Logging.Warn(LogCategory.VM, $"Target requested on GLOBAL event {tiedEvent.Type}");
                        entity = Entity.Null;
                    }
                    else {entity = tiedEvent.Target;}
                    break;
                
                case ConditionTarget.Source:
                    entity = tiedEvent.Source;
                    break;

                default:
                    entity = conditionOwner;
                    break;
            }

            return entity;
        }
    }
}
