using UnityEditor;
using UnityEngine;

public static class ClearTutorialPrefs
{
    private static readonly string[] StepNames =
    {
        "SelectPathCard", "BuildOnOre",
        "MineExplanation", "SpawnExplanation", "PathEditInfo",
        "SwitchToTowers", "SelectTower", "PlaceTower", "StartWave", "Complete"
    };

    [MenuItem("Tools/Clear Tutorial PlayerPrefs")]
    public static void Clear()
    {
        // Clear legacy keys
        PlayerPrefs.DeleteKey("tutorial_complete");
        PlayerPrefs.DeleteKey("tut_Welcome");
        PlayerPrefs.DeleteKey("tut_Mining");
        PlayerPrefs.DeleteKey("tut_SpawnPoints");
        PlayerPrefs.DeleteKey("tut_StartWave");
        PlayerPrefs.DeleteKey("tut_Upgrades");
        PlayerPrefs.DeleteKey("tut_Escape");
        PlayerPrefs.DeleteKey("tut_PlacePath");
        PlayerPrefs.DeleteKey("tut_RotationInfo");

        // Clear per-step keys
        foreach (var name in StepNames)
            PlayerPrefs.DeleteKey("tut_" + name);

        PlayerPrefs.Save();
        Debug.Log("Cleared all tutorial PlayerPrefs keys.");
    }
}
