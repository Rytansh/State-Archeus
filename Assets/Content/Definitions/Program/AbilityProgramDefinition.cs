using UnityEngine;
using System.Collections.Generic;
using DBUS.Battle.VM.Data;

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

    public float FloatValue;
}