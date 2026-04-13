using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Events.Runtime;
using Archeus.Battle.Events.Resolvers;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.VM.Execution;
using Archeus.Content.Registries;
using Archeus.Core.Debugging;

namespace Archeus.Battle.Systems.Events
{
    [UpdateInGroup(typeof(BattleSimulationGroup))]
    public partial struct BattleEventProcessingSystem : ISystem
    {
        private const int MAX_EXECUTIONS = 10000;
        private ComponentLookup<CharacterStats> characterStatsLookup;
        private ComponentLookup<CurrentHealth> characterHPLookup;
        private BufferLookup<BehaviourRuntimeState> behaviourStateLookup;

        public void OnCreate(ref SystemState state)
        {
            characterStatsLookup = state.GetComponentLookup<CharacterStats>(true);
            characterHPLookup = state.GetComponentLookup<CurrentHealth>();
            behaviourStateLookup = state.GetBufferLookup<BehaviourRuntimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // UPDATE LOOKUP TABLES
            characterStatsLookup.Update(ref state);
            characterHPLookup.Update(ref state);
            behaviourStateLookup.Update(ref state);

            foreach (var (mainEventQueue, chainedEventQueue, executionRequestQueue, battle) in SystemAPI.Query<DynamicBuffer<BattleEvent>, DynamicBuffer<ChainedBattleEvent>, DynamicBuffer<BehaviourExecutionRequest>>().WithAll<BattleTag>().WithEntityAccess())
            {
                // BASIC CHECKS
                if (mainEventQueue.Length == 0 && chainedEventQueue.Length == 0 && executionRequestQueue.Length == 0) continue;
                if (!SystemAPI.HasComponent<BattleContentRegistry>(battle)) continue;

                // CREATE RCIS
                BlobAssetReference<ContentBlobRegistry> battleRegistryReference = SystemAPI.GetComponent<BattleContentRegistry>(battle).BattleRegistryReference;
                BattleContext ctx = new BattleContext
                {
                    Battle = battle,
                    ChainBuffer = chainedEventQueue,
                    StatsLookup = characterStatsLookup,
                    HealthLookup = characterHPLookup,
                    BattleRegistryReference = battleRegistryReference
                };

                // CREATE EVENT STACK
                NativeList<EventFrame> eventStack = new NativeList<EventFrame>(64, Allocator.Temp);
                for (int i = mainEventQueue.Length - 1; i >= 0; i--)
                {
                    eventStack.Add(new EventFrame
                    {
                        Event = mainEventQueue[i],
                        Phase = BattleEventPhase.PreResolution,
                        PhaseStarted = false
                    });
                }
                mainEventQueue.Clear();


                int safetyCounter = 0;
                while (eventStack.Length > 0)
                {
                    // STOP DANGEROUS LOOP EXECUTION
                    if (++safetyCounter > MAX_EXECUTIONS)
                    {
                        Logging.Warn(LogCategory.Event, $"[Battle] Fatal error - TOO MANY EVENT EXECUTIONS - clearing.");
                        eventStack.Clear();
                        mainEventQueue.Clear();
                        chainedEventQueue.Clear();
                        executionRequestQueue.Clear();
                        break;
                    }

                    // GET LAST ELEMENT IN STACK
                    ref EventFrame frame = ref eventStack.ElementAt(eventStack.Length - 1);
                    
                    // PROCESS BEHAVIOURS
                    if (executionRequestQueue.Length > 0)
                    {
                        int last = executionRequestQueue.Length - 1;
                        var request = executionRequestQueue[last];
                        executionRequestQueue.RemoveAt(last);

                        if (!behaviourStateLookup.HasBuffer(request.Owner))
                        {
                            Logging.Warn(LogCategory.Event, "Missing BehaviourRuntimeState buffer.");
                            continue;
                        }

                        DynamicBuffer<BehaviourRuntimeState> stateBuffer = behaviourStateLookup[request.Owner];

                        BehaviourExecutor.Execute(request, ref frame.Event, ref ctx, stateBuffer);
                        continue;
                    }

                    // CHAINED EVENT INTERRUPTIONS
                    if (chainedEventQueue.Length > 0)
                    {
                        int last = chainedEventQueue.Length - 1;
                        BattleEvent evt = chainedEventQueue[last].Event;
                        chainedEventQueue.RemoveAt(last);

                        eventStack.Add(new EventFrame
                        {
                            Event = evt,
                            Phase = BattleEventPhase.PreResolution,
                            PhaseStarted = false
                        });

                        continue;
                    }

                    // PROCESS EVENT STACK
                    switch (frame.Phase)
                    {
                        case BattleEventPhase.PreResolution:
                        {
                            if (!frame.PhaseStarted)
                            {
                                BuildExecutionRequests(ref state, ref ctx, frame.Event, frame.Phase, executionRequestQueue);
                                frame.PhaseStarted = true;
                                continue;
                            }

                            frame.Phase = BattleEventPhase.Resolution;
                            frame.PhaseStarted = false;
                            continue;
                        }

                        case BattleEventPhase.Resolution:
                        {
                            if (!frame.PhaseStarted)
                            {
                                BattleEventResolver.Resolve(frame.Event, ref ctx);
                                frame.PhaseStarted = true;
                                continue; // allow chained events to run
                            }

                            frame.Phase = BattleEventPhase.PostResolution;
                            frame.PhaseStarted = false;
                            continue;
                        }

                        case BattleEventPhase.PostResolution:
                        {
                            if (!frame.PhaseStarted)
                            {
                                BuildExecutionRequests(ref state, ref ctx, frame.Event, frame.Phase, executionRequestQueue);
                                frame.PhaseStarted = true;
                                continue;
                            }

                            eventStack.RemoveAt(eventStack.Length - 1);
                            continue;
                        }
                    }
                }
                eventStack.Dispose();
            }
        }

        private void BuildExecutionRequests(ref SystemState state, ref BattleContext ctx, BattleEvent evt, BattleEventPhase phase, DynamicBuffer<BehaviourExecutionRequest> executionRequestQueue)
        {
            NativeList<BehaviourExecutionRequest> executionList = new NativeList<BehaviourExecutionRequest>(Allocator.Temp);

            foreach (var (behaviours, entity) in SystemAPI.Query<DynamicBuffer<BehaviourReference>>().WithEntityAccess())
            {
                TriggerCollector.CollectFromEntity(entity, behaviours, ref ctx, evt, phase, ref executionList);
            }

            executionList.Sort(new BehaviourExecutionComparer()); // SORTS IN PLACE

            for (int i = 0; i < executionList.Length; i++)
            {
                executionRequestQueue.Add(executionList[i]);
            }

            executionList.Dispose();
        }
    }
}
