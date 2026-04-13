using UnityEngine;
using System.Collections.Generic;
using Archeus.Gameplay.Stats;

[CreateAssetMenu(fileName = "SkillDefinition", menuName = "Definition/Skill")]
public class SkillDefinition : ScriptableObject
{
    public string ID;
    public string Name;
    public SkillRarity Rarity;
    public Speciality Speciality; 
    public SkillBaseStats SkillBaseStats;
    public int Duration;
    public string NormalAbilityID;
    public string DelayAndImprovementAbilityID;
    public List<string> BehaviourIDs;
}
[System.Serializable]
public struct SkillBaseStats
{
    public float Attack;
    public float Defense;
    public float Health;
}
