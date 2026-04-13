using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Events.Runtime;
using Archeus.Battle.Events.Definitions;
using Archeus.Content.Registries;
using Archeus.Content.Blobs;

namespace Archeus.Battle.VM.Execution
{
    public static class TriggerCollector
    {
        public static void CollectFromEntity(Entity entity, DynamicBuffer<BehaviourReference> behaviours, ref BattleContext ctx, in BattleEvent evt, BattleEventPhase phase, ref NativeList<BehaviourExecutionRequest> results)
        {
            ref ContentBlobRegistry registry = ref ctx.BattleRegistryReference.Value;

            for (int i = 0; i < behaviours.Length; i++)
            {
                int behaviourIndex = behaviours[i].BehaviourIndex;
                ref BehaviourDefinitionBlob behaviour = ref registry.Behaviours[behaviourIndex];

                CollectFromBehaviour(entity, i, behaviourIndex, ref behaviour, ref ctx, evt, phase, ref results);
            }
        }

        private static void CollectFromBehaviour(Entity entity, int registrationIndex, int behaviourIndex, ref BehaviourDefinitionBlob behaviour, ref BattleContext ctx, in BattleEvent evt, BattleEventPhase phase, ref NativeList<BehaviourExecutionRequest> results)
        {
            for (int triggerIndex = 0; triggerIndex < behaviour.Triggers.Length; triggerIndex++)
            {
                ref BehaviourTriggerBlob trigger = ref behaviour.Triggers[triggerIndex];

                if (!IsMatchingTrigger(ref trigger, evt, phase))
                    continue;

                if (!BehaviourConditionEvaluator.Evaluate(entity, evt, ref ctx, ref trigger))
                    continue;

                results.Add(new BehaviourExecutionRequest
                {
                    BehaviourIndex = behaviourIndex,
                    TriggerIndex = triggerIndex,
                    Priority = trigger.Priority,
                    RegistrationIndex = registrationIndex,

                    Owner = entity,
                    Source = evt.Source,
                    Target = evt.Target
                });
            }
        }

        private static bool IsMatchingTrigger(ref BehaviourTriggerBlob trigger, in BattleEvent evt, BattleEventPhase phase) { return trigger.EventType == evt.Type && trigger.Phase == phase; }
    }
}
