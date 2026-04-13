using UnityEngine;
using UnityEditor;

[CreateAssetMenu(menuName = "Definition/Ability Program (Text)")]
public class AbilityProgramTextDefinition : ScriptableObject
{
    [TextArea(20, 50)]
    public string Source;

    public AbilityProgramDefinition Output;

    [ContextMenu("Compile Program")]
    public void Compile()
    {
        if (Output == null || string.IsNullOrEmpty(Source))
        {
            Debug.LogWarning("Missing Output or Source");
            return;
        }

        Output.Instructions = AbilityProgramCompiler.Compile(Source);

        #if UNITY_EDITOR
        EditorUtility.SetDirty(Output);
        AssetDatabase.SaveAssets();
        #endif
        
        Debug.Log("Program compiled successfully!");
    }
}

[CustomEditor(typeof(AbilityProgramTextDefinition))]
public class AbilityProgramTextDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var script = (AbilityProgramTextDefinition)target;

        if (GUILayout.Button("Compile Program"))
        {
            script.Compile();
        }
    }
}