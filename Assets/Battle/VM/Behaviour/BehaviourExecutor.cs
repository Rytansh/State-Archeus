using Unity.Entities;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Events.Runtime;
using Archeus.Battle.Components.Stats;
using Archeus.Content.Registries;
using Archeus.Content.Blobs;

namespace Archeus.Battle.VM.Execution
{
    public static class BehaviourExecutor
    {
        public static void Execute(BehaviourExecutionRequest request, ref BattleEvent evt, ref BattleContext ctx, DynamicBuffer<BehaviourRuntimeState> stateBuffer)
        {
            ref ContentBlobRegistry registry = ref ctx.BattleRegistryReference.Value;

            ref BehaviourDefinitionBlob behaviour = ref registry.Behaviours[request.BehaviourIndex];
            ref BehaviourTriggerBlob trigger = ref behaviour.Triggers[request.TriggerIndex];
            int stateIndex = request.RegistrationIndex;

            int programIndex = trigger.VMProgramIndex;

            AbilityExecutionFrame frame = new AbilityExecutionFrame
            {
                ProgramIndex = programIndex,
                BehaviourOwner = request.Owner,
                Source = request.Source,
                Target = request.Target,
                InstructionPointer = 0
            };

            AbilityExecutionContext context = new AbilityExecutionContext
            {
                ChainedEventQueue = ctx.ChainBuffer,
                CharacterStatsLookup = ctx.StatsLookup,
                ContentRegistry = ctx.BattleRegistryReference,
                StateBuffer = stateBuffer,
                StateIndex = stateIndex
            };

            AbilityInterpreter.Execute(ref frame, ref context, ref evt);
        }
    }
}
