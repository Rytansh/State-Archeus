using UnityEngine;
using Archeus.Battle.VM.Programs;

[CreateAssetMenu(menuName = "Definition/Ability Program")]
public class AbilityProgramDefinition : ScriptableObject
{
    public string ID;

    [TextArea(20, 50)]
    public string Source;
}

[System.Serializable]  
public class InstructionDefinition 
{ 
    public AbilityOpcode Opcode; 
    public int A; 
    public int B; 
}