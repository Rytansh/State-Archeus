using Archeus.Battle.Data.Effects;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EffectDefinition", menuName = "Definition/Effect")]
public class EffectDefinition : ScriptableObject
{
    public string ID;   

    public List<StatModifierDefinition> StatModifiers;
    public StackingBehaviour StackBehaviour;
    public List<string> BehaviourIDs;
}

[System.Serializable]
public struct StatModifierDefinition
{
    public StatType StatType;
    public ModifierOperation ModifierType;
}



