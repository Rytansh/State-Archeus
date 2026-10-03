using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Events.Context;
using Archeus.Content.Blobs;
using Archeus.Content.Registries;
using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Battle.VM.Execution
{
    public static class BehaviourExecutor
    {
        public static AbilityExecutionResult Execute(
            BehaviourExecutionRequest request,
            ref BattleEvent evt,
            ref BattleContext ctx,
            DynamicBuffer<BehaviourRuntimeState> stateBuffer,
            out AbilityExecutionFrame frame
        )
        {
            ref ContentBlobRegistry registry = ref ctx.BattleRegistryReference.Value;

            ref BehaviourDefinitionBlob behaviour = ref registry.Behaviours[request.BehaviourIndex];

            ref BehaviourTriggerBlob trigger = ref behaviour.Triggers[request.TriggerIndex];

            int stateIndex = request.RegistrationIndex;
            int programIndex = trigger.VMProgramIndex;

            frame = default;

            if (programIndex < 0 || programIndex >= registry.AbilityPrograms.Length)
            {
                Logging.Warn(
                    LogCategory.Event,
                    $"Invalid VM program index {programIndex}. "
                        + $"AbilityPrograms.Length="
                        + $"{registry.AbilityPrograms.Length}"
                );

                return AbilityExecutionResult.Aborted;
            }

            frame = new AbilityExecutionFrame
            {
                ProgramIndex = programIndex,

                BehaviourOwner = request.Owner,
                Source = request.Source,
                Target = request.Target,

                InstructionPointer = 0,
                StepsExecuted = 0,
            };

            if (request.Target != Entity.Null)
            {
                frame.Targets.Add(request.Target);
            }

            return ExecuteFrame(
                ref frame,
                stateIndex,
                in request.EmissionContext,
                ref evt,
                ref ctx,
                stateBuffer
            );
        }

        private static AbilityExecutionResult ExecuteFrame(
            ref AbilityExecutionFrame frame,
            int stateIndex,
            in EventEmissionContext emissionContext,
            ref BattleEvent evt,
            ref BattleContext ctx,
            DynamicBuffer<BehaviourRuntimeState> stateBuffer
        )
        {
            AbilityExecutionContext context = new AbilityExecutionContext
            {
                ChainedEventQueue = ctx.ChainedEventQueue,
                CharacterStatsLookup = ctx.StatsLookup,
                ContentRegistry = ctx.BattleRegistryReference,
                ActionExecutionStates = ctx.ActionExecutionStates,
                OperationCounter = ctx.OperationCounter,
                StateBuffer = stateBuffer,
                StateIndex = stateIndex,
                BattleParticipants = ctx.Participants,
                TeamLookup = ctx.TeamLookup,
                CurrentHealthLookup = ctx.HealthLookup,
                EmissionContext = emissionContext,
            };

            return AbilityInterpreter.Execute(ref frame, ref context, ref evt);
        }

        public static AbilityExecutionResult Resume(
            ref AbilityExecutionContinuation continuation,
            ref BattleEvent evt,
            ref BattleContext ctx,
            DynamicBuffer<BehaviourRuntimeState> stateBuffer
        )
        {
            return ExecuteFrame(
                ref continuation.Frame,
                continuation.BehaviourStateIndex,
                in continuation.BaseEmissionContext,
                ref evt,
                ref ctx,
                stateBuffer
            );
        }
    }
}
