using Archeus.Game.Stats;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Definition/Character")]
public class CharacterDefinition : ScriptableObject
{
    public string ID;   
    public string Name;  
    public CharacterRarity Rarity;           
    public BattleType BattleType;   
    public Speciality Speciality;   

    public CharacterBaseStats CharacterBaseStats; 

    public string NormalAttackID;
    public string SuperchargedAttackID;  
    public string FinalTrumpSkillID;  
    public List<string> BehaviourIDs;
}
[System.Serializable]
public struct CharacterBaseStats
{
    public float MaxHealth;
    public float Attack;
    public float Defense;
    public float CritDMG;
    public float CritRATE;
    public float Reactivity;
    public float SustainPOWER;
    public float MagicalDMGBonus;
    public float PowerfulDMGBonus;
    public float TacticalDMGBonus;
    public float FlexibleDMGBonus;
    public float DOTDMGBonus;
    public float ALLDMGBonus;
}


