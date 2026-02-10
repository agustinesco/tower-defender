using UnityEditor;
using UnityEngine;

public static class ClearTutorialPrefs
{
    [MenuItem("Tools/Clear Tutorial PlayerPrefs")]
    public static void Clear()
    {
        PlayerPrefs.DeleteKey("tutorial_complete");
        PlayerPrefs.Save();
        Debug.Log("Cleared tutorial_complete PlayerPrefs key.");
    }
}
