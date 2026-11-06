using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Events.Context;
using Archeus.Battle.Scheduling;
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

            int stateIndex = request.IsActionProgram ? -1 : request.RegistrationIndex;
            int programIndex;
            if (request.IsActionProgram)
                programIndex = request.ProgramIndex;
            else
            {
                ref BehaviourDefinitionBlob behaviour = ref registry.Behaviours[
                    request.BehaviourIndex
                ];
                programIndex = behaviour.Triggers[request.TriggerIndex].VMProgramIndex;
            }

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
                InheritedSource = evt.ActionData.InheritedSource,
                ControlPriority = request.Priority,
                ControlOwnerID = request.OwnerRuntimeID,
                ControlRegistration = request.RegistrationIndex,
                ControlTrigger = request.TriggerIndex,
            };

            if (request.Target != Entity.Null)
            {
                frame.Targets.Add(request.Target);
            }
            if (request.IsActionProgram)
            {
                int execution = ActionResolution.Find(
                    ctx.ActionExecutionStates,
                    evt.ActionData.ActionExecutionID
                );
                if (
                    execution >= 0
                    && ctx.ActionExecutionStates[execution].Action.TargetMode
                        == ActionTargetMode.AllEnemies
                )
                {
                    frame.Targets.Clear();
                    Entity owner = ctx.Entities.GetComponentData<CardOwner>(request.Source).Player;
                    foreach (var participant in ctx.Participants)
                    {
                        Entity target = participant.Participant;
                        if (
                            !ActionQueueService.Belongs(ctx.Entities, target, ctx.Battle)
                            || !ctx.Entities.HasComponent<CardOwner>(target)
                        )
                            continue;
                        Entity targetOwner = ctx
                            .Entities.GetComponentData<CardOwner>(target)
                            .Player;
                        if (
                            targetOwner == owner
                            || !ActionQueueService.IsLivingFieldActor(
                                ctx.Entities,
                                target,
                                ctx.Battle
                            )
                        )
                            continue;
                        if (frame.Targets.Length >= frame.Targets.Capacity)
                            return AbilityExecutionResult.Aborted;
                        frame.Targets.Add(target);
                    }
                }
            }

            AbilityInterpreterTooling.SortTargets(ref frame, ctx.Entities);
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
                Entities = ctx.Entities,
                Battle = ctx.Battle,
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
