using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Events;
using DBUS.Battle.Components.Combat;
using DBUS.Battle.Components.Ownership;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.VM.Data;
using DBUS.Battle.VM.Systems;
using DBUS.Battle.Resolvers;

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
            if (mainEventQueue.Length == 0 && chainedEventQueue.Length == 0 && executionRequestQueue.Length == 0) {continue;}
            BattleSimulationContext ctx = new BattleSimulationContext
            {
                Battle = battle,
                ChainBuffer = chainedEventQueue,
                StatsLookup = characterStatsLookup,
                HealthLookup = characterHPLookup
            };
            int safetyCounter = 0;

            var registryRef = SystemAPI.GetSingleton<ContentBlobRegistryComponent>().BlobRegistryReference;
            ref var registry = ref registryRef.Value;

            while (true)
            {
                if (++safetyCounter > MAX_EXECUTIONS)
                {
                    Logging.Warning($"[Battle] Fatal error - TOO MANY EVENT EXECUTIONS - clearing event queue for {battle}.");
                    mainEventQueue.Clear();
                    chainedEventQueue.Clear();
                    executionRequestQueue.Clear();
                    break;
                }

                // 1. Chained events (highest priority)
                if (chainedEventQueue.Length > 0)
                {
                    var evt = chainedEventQueue[0].Event;
                    chainedEventQueue.RemoveAt(0);

                    Dispatch(ref state, ref registry, ctx, evt, executionRequestQueue);
                    continue;
                }

                // 2. Behaviour executions
                if (executionRequestQueue.Length > 0)
                {
                    var request = executionRequestQueue[0];
                    executionRequestQueue.RemoveAt(0);

                    ExecuteBehaviour(ref state, registryRef, battle, chainedEventQueue, request);
                    continue;
                }

                // 3. Main events (lowest priority)
                if (mainEventQueue.Length > 0)
                {
                    var evt = mainEventQueue[0];
                    mainEventQueue.RemoveAt(0);

                    Dispatch(ref state, ref registry, ctx, evt, executionRequestQueue);
                    continue;
                }

                // Nothing left → we're done
                break;
            }
        }
    }


    private void Dispatch(ref SystemState state, ref ContentBlobRegistry registry, BattleSimulationContext ctx, BattleEvent evt, DynamicBuffer<BehaviourExecutionRequest> executionRequestQueue)
    {

        var tempList = new NativeList<BehaviourExecutionRequest>(Allocator.Temp);

        ResolveEvent(ref state, ref ctx, evt);

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

    private void ExecuteBehaviour(ref SystemState state, BlobAssetReference<ContentBlobRegistry> registryRef, Entity battle, DynamicBuffer<ChainedBattleEvent> chainedEventQueue, BehaviourExecutionRequest request)
    {
        LogExecution(ref state, battle, request);
        ref var registry = ref registryRef.Value;

        ref var behaviour = ref registry.Behaviours[request.BehaviourIndex];
        ref var trigger = ref behaviour.Triggers[request.TriggerIndex];

        int programIndex = trigger.VMProgramIndex;

        AbilityExecutionFrame frame = new AbilityExecutionFrame
        {
            ProgramIndex = programIndex,
            Source = request.SourceEvent.Source,
            Target = request.SourceEvent.Target,
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
        LogEvent(ref state, ctx.Battle, evt);
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

    private void LogExecution(ref SystemState state, Entity battle, BehaviourExecutionRequest request)
    {
        var counter = SystemAPI.GetComponentRW<BattleExecutionCounter>(battle);
        var rng = SystemAPI.GetComponent<BattleRNG>(battle);
        var logQueue = SystemAPI.GetBuffer<BattleExecutionLog>(battle);

        logQueue.Add(new BattleExecutionLog
        {
            StepIndex = counter.ValueRO.Value,
            EventType = request.SourceEvent.Type,
            BehaviourID = request.BehaviourIndex,
            RngStateA = rng.StateA,
            RngStateB = rng.StateB
        });

        counter.ValueRW.Value++;

        Logging.System($"[Battle {battle.Index}] Step {counter.ValueRO.Value} | " + $"Event:{request.SourceEvent.Type} | " + $"Behaviour:{request.BehaviourIndex} | " + $"RNG:({rng.StateA},{rng.StateB})");
    }
    private void LogEvent(ref SystemState state, Entity battle, BattleEvent evt)
    {
        var counter = SystemAPI.GetComponentRW<BattleExecutionCounter>(battle);
        var logQueue = SystemAPI.GetBuffer<BattleExecutionLog>(battle);

        logQueue.Add(new BattleExecutionLog
        {
            StepIndex = counter.ValueRO.Value,
            EventType = evt.Type,
            BehaviourID = 0,
            RngStateA = 0,
            RngStateB = 0
        });

        counter.ValueRW.Value++;

        Logging.System(
            $"[Battle {battle.Index}] Step {counter.ValueRO.Value} | " +
            $"Event:{evt.Type}"
        );
    }
}
