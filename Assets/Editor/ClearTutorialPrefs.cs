using UnityEditor;
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
    }
}
