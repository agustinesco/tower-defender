using UnityEditor;
using UnityEngine;

public class SetBuildScenes
{
    [MenuItem("Tools/Set Build Scenes")]
    public static void Set()
    {
        EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true)
        };
        Debug.Log("Build scenes set: MainMenu (0), SampleScene (1)");
    }
}
