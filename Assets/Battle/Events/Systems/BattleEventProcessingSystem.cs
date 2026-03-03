using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Events;
using DBUS.Battle.Components.Ownership;
using DBUS.Battle.Components.Determinism;

[UpdateInGroup(typeof(BattleSimulationGroup))]
public partial struct BattleEventProcessingSystem : ISystem
{
    private const int MAX_EXECUTIONS = 10000;
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (processingState, battle)
            in SystemAPI.Query<RefRW<BattleEventProcessingState>>()
                        .WithAll<BattleTag>()
                        .WithEntityAccess())
        {
            if (processingState.ValueRO.IsProcessing)
                continue;

            processingState.ValueRW.IsProcessing = true;

            var eventBuffer = SystemAPI.GetBuffer<BattleEventBuffer>(battle);
            var execBuffer = SystemAPI.GetBuffer<BehaviorExecutionRequest>(battle);
            var triggers = SystemAPI.GetBuffer<RegisteredTrigger>(battle);

            int safetyCounter = 0;

            while (eventBuffer.Length > 0 || execBuffer.Length > 0)
            {
                if (++safetyCounter > MAX_EXECUTIONS)
                {
                    Logging.Warning($"[Battle] Fatal error - TOO MANY EVENT EXECUTIONS - clearing event queue for {battle}.");
                    eventBuffer.Clear();
                    execBuffer.Clear();
                    break;
                }

                while (eventBuffer.Length > 0)
                {
                    var evt = eventBuffer[0].Value;
                    eventBuffer.RemoveAt(0);

                    Dispatch(ref state, battle, evt, triggers, execBuffer);
                }

                while (execBuffer.Length > 0)
                {
                    var request = execBuffer[0];
                    execBuffer.RemoveAt(0);

                    ExecuteBehaviour(ref state, battle, request);
                }
            }

            processingState.ValueRW.IsProcessing = false;
        }
    }


    private void Dispatch(ref SystemState state, Entity battle, BattleEvent evt, DynamicBuffer<RegisteredTrigger> triggers, DynamicBuffer<BehaviorExecutionRequest> execBuffer)
    {
        var tempList = new NativeList<BehaviorExecutionRequest>(Allocator.Temp);

        for (int i = 0; i < triggers.Length; i++)
        {
            var trigger = triggers[i];

            if (trigger.EventType != evt.Type)
                continue;

            tempList.Add(new BehaviorExecutionRequest
            {
                BehaviourID = trigger.BehaviourID,
                Owner = trigger.Owner,
                SourceEvent = evt,
                Priority = trigger.Priority,
                RegistrationIndex = trigger.RegistrationIndex
            });
        }

        // Sort by priority (higher first) then registration index
        tempList.Sort(new BehaviorExecutionComparer());

        for (int i = 0; i < tempList.Length; i++)
        {
            execBuffer.Add(tempList[i]);
        }

        tempList.Dispose();
    }

    private void ExecuteBehaviour(ref SystemState state, Entity battle, BehaviorExecutionRequest request)
    {
        var counter = SystemAPI.GetComponentRW<BattleExecutionCounter>(battle);
        var rng = SystemAPI.GetComponent<BattleRNG>(battle);

        var logBuffer = SystemAPI.GetBuffer<BattleExecutionLog>(battle);

        logBuffer.Add(new BattleExecutionLog
        {
            StepIndex = counter.ValueRO.Value,
            EventType = request.SourceEvent.Type,
            BehaviourID = request.BehaviourID,
            RngStateA = rng.StateA,
            RngStateB = rng.StateB
        });

        counter.ValueRW.Value++;

        Logging.System(
            $"[Battle {battle.Index}] Step {counter.ValueRO.Value} | " +
            $"Evt:{request.SourceEvent.Type} | " +
            $"Beh:{request.BehaviourID} | " +
            $"RNG:({rng.StateA},{rng.StateB})"
        );
    }
}
