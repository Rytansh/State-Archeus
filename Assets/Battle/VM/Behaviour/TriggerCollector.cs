using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Context;
using Archeus.Content.Blobs;
using Archeus.Content.Registries;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.VM.Execution
{
    public static class TriggerCollector
    {
        public static void CollectFromEntity(
            Entity entity,
            DynamicBuffer<BehaviourReference> behaviours,
            ref BattleContext ctx,
            in BattleEvent evt,
            uint currentFrameID,
            BattleEventPhase phase,
            ref NativeList<BehaviourExecutionRequest> results
        )
        {
            if (
                evt.Scope == BattleEventScope.Targeted
                && entity != evt.Source
                && entity != evt.Target
            )
            {
                return;
            }

            ref ContentBlobRegistry registry = ref ctx.BattleRegistryReference.Value;

            for (int i = 0; i < behaviours.Length; i++)
            {
                int behaviourIndex = behaviours[i].BehaviourIndex;

                ref BehaviourDefinitionBlob behaviour = ref registry.Behaviours[behaviourIndex];

                CollectFromBehaviour(
                    entity,
                    i,
                    behaviourIndex,
                    ref behaviour,
                    ref ctx,
                    in evt,
                    currentFrameID,
                    phase,
                    ref results
                );
            }
        }

        private static void CollectFromBehaviour(
            Entity entity,
            int registrationIndex,
            int behaviourIndex,
            ref BehaviourDefinitionBlob behaviour,
            ref BattleContext ctx,
            in BattleEvent evt,
            uint currentFrameID,
            BattleEventPhase phase,
            ref NativeList<BehaviourExecutionRequest> results
        )
        {
            for (int triggerIndex = 0; triggerIndex < behaviour.Triggers.Length; triggerIndex++)
            {
                ref BehaviourTriggerBlob trigger = ref behaviour.Triggers[triggerIndex];

                if (!IsMatchingTrigger(ref trigger, in evt, phase))
                {
                    continue;
                }

                if (!MatchesOwnerType(entity, trigger.OwnerType, in evt))
                {
                    continue;
                }

                if (!BehaviourConditionEvaluator.Evaluate(entity, evt, ref ctx, ref trigger))
                {
                    continue;
                }

                ushort executionGeneration;

                switch (phase)
                {
                    // Interceptors remain within the current
                    // causal generation.
                    case BattleEventPhase.PreResolution:
                    {
                        executionGeneration = evt.StructuralData.Generation;

                        break;
                    }

                    // Reactions are consequences and therefore
                    // execute in the next causal generation.
                    case BattleEventPhase.PostResolution:
                    {
                        executionGeneration = checked((ushort)(evt.StructuralData.Generation + 1));

                        break;
                    }

                    // Trigger collection should never be performed
                    // during Resolution.
                    default:
                    {
                        continue;
                    }
                }

                EventEmissionContext emissionContext = new EventEmissionContext
                {
                    StructuralData = new EventStructuralData
                    {
                        GroupID = evt.StructuralData.GroupID,
                        Generation = executionGeneration,
                        ParentFrameID = currentFrameID,
                    },
                    ActionData = evt.ActionData,
                    ExecutionData = evt.ExecutionData,
                    CurrentFrameID = currentFrameID,
                };

                results.Add(
                    new BehaviourExecutionRequest
                    {
                        BehaviourIndex = behaviourIndex,

                        TriggerIndex = triggerIndex,

                        Priority = trigger.Priority,

                        RegistrationIndex = registrationIndex,

                        Owner = entity,

                        Source = evt.Source,

                        Target = evt.Target,

                        EmissionContext = emissionContext,
                    }
                );
            }
        }

        private static bool IsMatchingTrigger(
            ref BehaviourTriggerBlob trigger,
            in BattleEvent evt,
            BattleEventPhase phase
        )
        {
            return trigger.EventType == evt.Type && trigger.Phase == phase;
        }

        private static bool MatchesOwnerType(
            Entity owner,
            TriggerOwnerType ownerType,
            in BattleEvent evt
        )
        {
            switch (ownerType)
            {
                case TriggerOwnerType.Source:
                    return owner == evt.Source;

                case TriggerOwnerType.Target:
                    return owner == evt.Target;

                case TriggerOwnerType.Any:
                    return true;

                default:
                    return false;
            }
        }
    }
}
