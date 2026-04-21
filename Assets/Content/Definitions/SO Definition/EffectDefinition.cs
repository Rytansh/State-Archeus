using Archeus.Battle.Effects;
using Archeus.Game.Stats;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EffectDefinition", menuName = "Definition/Effect")]
public class EffectDefinition : ScriptableObject
{
    public string ID;   
    public StackingType StackingType;

    public int Duration;
    public List<StatModifierData> Modifiers;
}

[System.Serializable]
public struct StatModifierData
{
    public StatType StatType;
    public StatModifierType ModifierType;
    public float Value;
}


