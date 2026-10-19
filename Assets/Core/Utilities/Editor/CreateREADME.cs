using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateREADME
{
    [MenuItem("Assets/Create/Utilities/Documentation/README")]
    private static void Create()
    {
        string folder = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (!AssetDatabase.IsValidFolder(folder))
        {
            folder = Path.GetDirectoryName(folder);
        }

        if (string.IsNullOrEmpty(folder))
        {
            folder = "Assets";
        }

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/README.md");

        File.WriteAllText(path, "# Documentation\n\nDescribe this subsystem here.\n");

        AssetDatabase.ImportAsset(path);

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
    }
}
