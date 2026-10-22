using System.Collections.Generic;
using Archeus.Gameplay.Stats;
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

    public float SustainPOWER;
    public float DOTDMGBonus;

    public float Authority;
    public float Reactivity;
    public float Stability;
    public float Observation;
    public float Initiative;

    public float ALLDMGBonus;
}
