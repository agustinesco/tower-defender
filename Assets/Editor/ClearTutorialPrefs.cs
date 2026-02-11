using UnityEditor;
using UnityEngine;

public static class ClearTutorialPrefs
{
    private static readonly string[] StepNames =
    {
        "Welcome", "Mining", "SpawnPoints", "PlaceTower",
        "StartWave", "Upgrades", "Escape"
    };

    [MenuItem("Tools/Clear Tutorial PlayerPrefs")]
    public static void Clear()
    {
        // Clear legacy key
        PlayerPrefs.DeleteKey("tutorial_complete");

        // Clear per-step keys
        foreach (var name in StepNames)
            PlayerPrefs.DeleteKey("tut_" + name);

        PlayerPrefs.Save();
        Debug.Log("Cleared all tutorial PlayerPrefs keys.");
    }
}
