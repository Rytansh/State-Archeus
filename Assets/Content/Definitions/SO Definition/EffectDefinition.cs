using Archeus.Gameplay.Stats;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EffectDefinition", menuName = "Definition/Effect")]
public class EffectDefinition : ScriptableObject
{
    public string ID;   
    public List<string> BehaviourIDs;
}



