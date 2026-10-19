using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class AutoSceneStart
{
    static AutoSceneStart()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            "Assets/Scenes/BootstrapScene.unity"
        );
    }
}
