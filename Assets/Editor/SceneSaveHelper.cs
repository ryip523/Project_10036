using UnityEditor;
using UnityEditor.SceneManagement;

public class SceneSaveHelper
{
    [MenuItem("Tools/Load MainMenu Scene")]
    public static void LoadMainMenu()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        UnityEngine.Debug.Log("Loaded MainMenu scene.");
    }

    [MenuItem("Tools/Load Angel Scene")]
    public static void LoadAngelScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Angel.unity");
        UnityEngine.Debug.Log("Loaded Angel scene.");
    }
}
