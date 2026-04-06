using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BehaviourDefinition", menuName = "Definition/Behaviour")]
public class BehaviourDefinition : ScriptableObject
{
    public string ID;
    public List<BehaviourTriggerDefinition> Triggers = new();
}

[System.Serializable]
public class BehaviourTriggerDefinition
{
    public BattleEventType EventType;
    public BattleEventPhase Phase;

    [Tooltip("VM Program ID to execute")]
    public string VMProgramID;
    public int Priority;
    public List<EventConditionDefinition> Conditions = new();
}

[System.Serializable]
public class EventConditionDefinition
{
    public ConditionType Type;
    public ConditionTarget Target;

    [Tooltip("Used for numeric comparisons (%, stacks, etc.)")]
    public float Value;
}


