using UnityEngine;
using System.Collections.Generic;
using Archeus.Battle.VM.Programs;

[CreateAssetMenu(menuName = "Definition/Ability Program")]
public class AbilityProgramDefinition : ScriptableObject
{
    public string ID;
    public List<InstructionDefinition> Instructions;
}



[System.Serializable]
public class InstructionDefinition
{
    public AbilityOpcode Opcode;

    public int A;
    public int B;
}