using UnityEditor;
using UnityEngine;

public class BuildSettingsSetup
{
    [MenuItem("Tools/Setup Build Settings")]
    public static void SetupBuildSettings()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity",   true),
            new EditorBuildSettingsScene("Assets/Scenes/Angel.unity", true),
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("Build Settings updated: MainMenu (index 0), Angel (index 1).");
    }
}
