using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Events;
using DBUS.Battle.Components.Combat;
using DBUS.Battle.Components.Ownership;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.VM.Data;
using DBUS.Battle.VM.Systems;
using DBUS.Battle.Resolvers;
using UnityEngine.InputSystem;

[UpdateInGroup(typeof(BattleSimulationGroup))]
public partial struct BattleEventProcessingSystem : ISystem
{
    private const int MAX_EXECUTIONS = 10000;
    private ComponentLookup<CharacterStats> characterStatsLookup;
    private ComponentLookup<CurrentHealth> characterHPLookup;
    public void OnCreate(ref SystemState state)
    {
        characterStatsLookup = state.GetComponentLookup<CharacterStats>(true);
        characterHPLookup = state.GetComponentLookup<CurrentHealth>();
    }
    public void OnUpdate(ref SystemState state)
    {
        characterStatsLookup.Update(ref state);
        characterHPLookup.Update(ref state);

        foreach (var (mainEventQueue, chainedEventQueue, executionRequestQueue, battle) in SystemAPI.Query<DynamicBuffer<BattleEvent>, DynamicBuffer<ChainedBattleEvent>, DynamicBuffer<BehaviourExecutionRequest>>().WithAll<BattleTag>().WithEntityAccess())
        {
            if (mainEventQueue.Length == 0 && chainedEventQueue.Length == 0 && executionRequestQueue.Length == 0) continue;
            if (!SystemAPI.HasSingleton<ContentBlobRegistryComponent>()) continue;

            int safetyCounter = 0;

            BattleSimulationContext ctx = new BattleSimulationContext
            {
                Battle = battle,
                ChainBuffer = chainedEventQueue,
                StatsLookup = characterStatsLookup,
                HealthLookup = characterHPLookup
            };

            var registryRef = SystemAPI.GetSingleton<ContentBlobRegistryComponent>().BlobRegistryReference;
            ref var registry = ref registryRef.Value;

            NativeList<EventFrame> eventStack = new NativeList<EventFrame>(64, Allocator.Temp);

            // Seed stack from main queue
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

            while (eventStack.Length > 0)
            {
                if (++safetyCounter > MAX_EXECUTIONS)
                {
                    Logging.Warning($"[Battle] Fatal error - TOO MANY EVENT EXECUTIONS - clearing.");
                    eventStack.Clear();
                    chainedEventQueue.Clear();
                    executionRequestQueue.Clear();
                    break;
                }

                ref var frame = ref eventStack.ElementAt(eventStack.Length - 1);

                // 🔥 1. VM INTERRUPTS (always first)
                if (executionRequestQueue.Length > 0)
                {
                    int last = executionRequestQueue.Length - 1;
                    var request = executionRequestQueue[last];
                    executionRequestQueue.RemoveAt(last);

                    ExecuteBehaviour(ref state, registryRef, battle, chainedEventQueue, request);
                    continue;
                }

                // 🔥 2. CHILD EVENTS (push immediately → stack)
                if (chainedEventQueue.Length > 0)
                {
                    int last = chainedEventQueue.Length - 1;
                    var evt = chainedEventQueue[last].Event;
                    chainedEventQueue.RemoveAt(last);

                    eventStack.Add(new EventFrame
                    {
                        Event = evt,
                        Phase = BattleEventPhase.PreResolution,
                        PhaseStarted = false
                    });

                    continue;
                }

                // 🔥 3. PROCESS CURRENT FRAME
                switch (frame.Phase)
                {
                    case BattleEventPhase.PreResolution:
                    {
                        if (!frame.PhaseStarted)
                        {
                            Logging.System($"DISPATCH → {frame.Event.Type} | Pre");

                            CollectBehaviours(ref state, ref registry, frame.Event, frame.Phase, executionRequestQueue);
                            frame.PhaseStarted = true;
                            continue; // wait for VM / children
                        }

                        // Only advance when no work remains
                        frame.Phase = BattleEventPhase.Resolution;
                        frame.PhaseStarted = false;
                        continue;
                    }

                    case BattleEventPhase.Resolution:
                    {
                        if (!frame.PhaseStarted)
                        {
                            Logging.System($"DISPATCH → {frame.Event.Type} | Resolution");

                            ResolveEvent(ref state, ref ctx, frame.Event);
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
                            Logging.System($"DISPATCH → {frame.Event.Type} | Post");
                            CollectBehaviours(ref state, ref registry, frame.Event, frame.Phase, executionRequestQueue);
                            frame.PhaseStarted = true;
                            continue;
                        }

                        // ✅ Fully done → pop
                        eventStack.RemoveAt(eventStack.Length - 1);
                        continue;
                    }
                }
            }
            eventStack.Dispose();
        }
    }

    private void ExecuteBehaviour(ref SystemState state, BlobAssetReference<ContentBlobRegistry> registryRef, Entity battle, DynamicBuffer<ChainedBattleEvent> chainedEventQueue, BehaviourExecutionRequest request)
    {
        ref var registry = ref registryRef.Value;

        ref var behaviour = ref registry.Behaviours[request.BehaviourIndex];
        ref var trigger = ref behaviour.Triggers[request.TriggerIndex];

        int programIndex = trigger.VMProgramIndex;

        AbilityExecutionFrame frame = new AbilityExecutionFrame
        {
            ProgramIndex = programIndex,
            Source = request.Owner,
            Target = request.Owner,
            InstructionPointer = 0
        };


        AbilityExecutionContext context = new AbilityExecutionContext
        {
            ChainedEventQueue = chainedEventQueue,
            CharacterStatsLookup = characterStatsLookup,
            ContentRegistry = registryRef
        };

        AbilityInterpreter.Execute(ref frame, ref context);
    }

    private void ResolveEvent(ref SystemState state, ref BattleSimulationContext ctx, BattleEvent evt)
    {
        switch (evt.Type)
        {
            case BattleEventType.DamageRequested:
                DamageRequestResolver.Resolve(ref ctx, evt);
                break;

            // future resolvers
            // case BattleEventType.HealRequested:
            // case BattleEventType.ApplyBuff:
        }
    }

    private void CollectBehaviours(ref SystemState state, ref ContentBlobRegistry registry, BattleEvent evt, BattleEventPhase phase, DynamicBuffer<BehaviourExecutionRequest> executionRequestQueue)
    {
        var tempList = new NativeList<BehaviourExecutionRequest>(Allocator.Temp);

        foreach (var (behaviours, entity) in SystemAPI.Query<DynamicBuffer<BehaviourReference>>().WithEntityAccess())
        {
            for (int i = 0; i < behaviours.Length; i++)
            {
                int behaviourIndex = behaviours[i].BehaviourIndex;
                ref var behaviour = ref registry.Behaviours[behaviourIndex];

                for (int t = 0; t < behaviour.Triggers.Length; t++)
                {
                    ref var trigger = ref behaviour.Triggers[t];

                    if (trigger.EventType != evt.Type)
                        continue;

                    if (trigger.Phase != phase)
                        continue;

                    tempList.Add(new BehaviourExecutionRequest
                    {
                        BehaviourIndex = behaviourIndex,
                        TriggerIndex = t,
                        Owner = entity,
                        SourceEvent = evt,
                        Priority = trigger.Priority,
                        RegistrationIndex = i
                    });
                }
            }
        }

        tempList.Sort(new BehaviourExecutionComparer());

        for (int i = 0; i < tempList.Length; i++)
        {
            executionRequestQueue.Add(tempList[i]);
        }

        tempList.Dispose();
    }

}
