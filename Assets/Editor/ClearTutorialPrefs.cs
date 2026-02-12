using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TowerDefense.Core;

public static class ClearTutorialPrefs
{
    [MenuItem("Tools/Clear Tutorial Progress")]
    public static void Clear()
    {
        JsonSaveSystem.Load();
        JsonSaveSystem.ClearTutorialSteps();
        JsonSaveSystem.Save();
        Debug.Log("Cleared all tutorial progress from save file.");
    }

    [MenuItem("Tools/Delete All Save Data")]
    public static void DeleteAll()
    {
        JsonSaveSystem.DeleteAll();
        Debug.Log("Deleted all save data.");

        if (Application.isPlaying)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
        else
        {
            string mainMenuPath = "Assets/Scenes/MainMenu.unity";
            if (System.IO.File.Exists(mainMenuPath))
                EditorSceneManager.OpenScene(mainMenuPath);
        }
    }
}
