using Unity.Entities;
using DBUS.Battle.Components.Events;

public struct BattleExecutionLog : IBufferElementData
{
    public int StepIndex;               // Execution order
    public BattleEventType EventType;
    public int BehaviourID;
    public ulong RngStateA;
    public ulong RngStateB;
}
