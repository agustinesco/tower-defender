using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class TextToTMPMigrator : MonoBehaviour
{
    [MenuItem("Tools/Migrate Text to TMP")]
    static void MigrateAllTextToTMP()
    {
        var allText = Resources.FindObjectsOfTypeAll<Text>();
        int count = 0;

        // Collect data before modifying
        var migrations = new List<(GameObject go, string text, int fontSize, Color color, int alignment, int fontStyle, bool raycastTarget, bool richText)>();

        foreach (var t in allText)
        {
            if (t == null) continue;
            var go = t.gameObject;
            // Skip if already has TMP
            if (go.GetComponent<TextMeshProUGUI>() != null) continue;
            // Skip prefab assets (only migrate scene instances)
            if (PrefabUtility.IsPartOfPrefabAsset(go)) continue;

            migrations.Add((go, t.text, t.fontSize, t.color, (int)t.alignment, (int)t.fontStyle, t.raycastTarget, t.supportRichText));
        }

        foreach (var m in migrations)
        {
            Undo.RegisterCompleteObjectUndo(m.go, "Migrate Text to TMP");

            // Remove old Text
            var oldText = m.go.GetComponent<Text>();
            if (oldText != null)
                Undo.DestroyObjectImmediate(oldText);

            // Remove CanvasRenderer if leftover (TMP adds its own)
            // Actually TMP needs CanvasRenderer, leave it

            // Add TMP
            var tmp = Undo.AddComponent<TextMeshProUGUI>(m.go);
            tmp.text = m.text;
            tmp.fontSize = m.fontSize;
            tmp.color = m.color;
            tmp.raycastTarget = m.raycastTarget;
            tmp.richText = m.richText;

            // Map alignment
            tmp.alignment = MapAlignment(m.alignment);

            // Map font style
            if (m.fontStyle == 1) // Bold
                tmp.fontStyle = FontStyles.Bold;
            else if (m.fontStyle == 2) // Italic
                tmp.fontStyle = FontStyles.Italic;
            else if (m.fontStyle == 3) // BoldAndItalic
                tmp.fontStyle = FontStyles.Bold | FontStyles.Italic;

            count++;
        }

        // Now re-wire SerializeField references
        RewireHUDController();
        RewireTutorialManager();
        RewirePieceHandUI();
        RewireMainSceneController();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Migrated {count} Text components to TextMeshProUGUI");
    }

    static TextAlignmentOptions MapAlignment(int textAnchor)
    {
        switch (textAnchor)
        {
            case 0: return TextAlignmentOptions.TopLeft;
            case 1: return TextAlignmentOptions.Top;
            case 2: return TextAlignmentOptions.TopRight;
            case 3: return TextAlignmentOptions.Left;
            case 4: return TextAlignmentOptions.Center;
            case 5: return TextAlignmentOptions.Right;
            case 6: return TextAlignmentOptions.BottomLeft;
            case 7: return TextAlignmentOptions.Bottom;
            case 8: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    static void RewireHUDController()
    {
        var hud = Resources.FindObjectsOfTypeAll<TowerDefense.UI.HUDController>();
        if (hud.Length == 0) return;

        var so = new SerializedObject(hud[0]);

        TryWireByPath(so, "livesBarText", "GameHudCanvas/SafeArea/DynamicUI/LivesBar/Text");
        TryWireByPath(so, "currencyText", "GameHudCanvas/SafeArea/DynamicUI/ResourcePanel/Row_Gold/Text");
        TryWireByPath(so, "ironOreText", "GameHudCanvas/SafeArea/DynamicUI/ResourcePanel/Row_Iron/Text");
        TryWireByPath(so, "gemsText", "GameHudCanvas/SafeArea/DynamicUI/ResourcePanel/Row_Gems/Text");
        TryWireByPath(so, "florpusText", "GameHudCanvas/SafeArea/DynamicUI/ResourcePanel/Row_Florpus/Text");
        TryWireByPath(so, "adamantiteText", "GameHudCanvas/SafeArea/DynamicUI/ResourcePanel/Row_Adam/Text");
        TryWireByPath(so, "buildTimerText", "GameHudCanvas/SafeArea/DynamicUI/BuildTimer");
        TryWireByPath(so, "escapeButtonText", "GameHudCanvas/SafeArea/StaticUI/BottomRightButtons/EscapeButton/Text");

        so.ApplyModifiedProperties();
        Debug.Log("HUDController SerializeField references rewired");
    }

    static void RewireTutorialManager()
    {
        var tm = Resources.FindObjectsOfTypeAll<TowerDefense.Core.TutorialManager>();
        if (tm.Length == 0) return;

        var so = new SerializedObject(tm[0]);
        TryWireByPath(so, "messageText", "TutorialManager/SafeArea/TutorialPanel/Message");
        so.ApplyModifiedProperties();
        Debug.Log("TutorialManager SerializeField references rewired");
    }

    static void RewirePieceHandUI()
    {
        var phui = Resources.FindObjectsOfTypeAll<TowerDefense.UI.PieceHandUI>();
        if (phui.Length == 0) return;

        var so = new SerializedObject(phui[0]);
        TryWireByPath(so, "tooltipText", "GameHudCanvas/SafeArea/PieceHandUI/PieceHandPanel/Tooltip/TooltipText");
        so.ApplyModifiedProperties();
        Debug.Log("PieceHandUI SerializeField references rewired");
    }

    static void RewireMainSceneController()
    {
        var msc = Resources.FindObjectsOfTypeAll<TowerDefense.UI.MainSceneController>();
        if (msc.Length == 0) return;

        var so = new SerializedObject(msc[0]);
        TryWireByPath(so, "questResourceLabel", "MainMenuCanvas/SafeArea/QuestPanel/QuestResources");
        so.ApplyModifiedProperties();
        Debug.Log("MainSceneController SerializeField references rewired");
    }

    static void TryWireByPath(SerializedObject so, string propertyName, string path)
    {
        var go = FindByPath(path);
        if (go == null)
        {
            Debug.LogWarning($"Could not find GameObject at path: {path}");
            return;
        }

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            Debug.LogWarning($"No TextMeshProUGUI on {path}");
            return;
        }

        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogWarning($"Property {propertyName} not found");
            return;
        }

        prop.objectReferenceValue = tmp;
        Debug.Log($"Wired {propertyName} -> {path}");
    }

    [MenuItem("Tools/Migrate Prefab Text to TMP")]
    static void MigratePrefabTextToTMP()
    {
        string[] prefabPaths = new[]
        {
            "Assets/Prefabs/UI/Game/CardPrefab.prefab",
            "Assets/Prefabs/UI/Game/UpgradeCardPrefab.prefab"
        };

        foreach (var path in prefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Prefab not found: {path}");
                continue;
            }

            var textComponents = prefab.GetComponentsInChildren<Text>(true);
            if (textComponents.Length == 0)
            {
                Debug.Log($"No Text components in {path}");
                continue;
            }

            // Open prefab for editing
            var prefabRoot = PrefabUtility.LoadPrefabContents(path);
            var texts = prefabRoot.GetComponentsInChildren<Text>(true);

            var migrations = new List<(GameObject go, string text, int fontSize, Color color, int alignment, int fontStyle, bool raycastTarget, bool richText)>();

            foreach (var t in texts)
            {
                if (t == null) continue;
                if (t.gameObject.GetComponent<TextMeshProUGUI>() != null) continue;
                migrations.Add((t.gameObject, t.text, t.fontSize, t.color, (int)t.alignment, (int)t.fontStyle, t.raycastTarget, t.supportRichText));
            }

            foreach (var m in migrations)
            {
                var oldText = m.go.GetComponent<Text>();
                if (oldText != null)
                    Object.DestroyImmediate(oldText);

                var tmp = m.go.AddComponent<TextMeshProUGUI>();
                tmp.text = m.text;
                tmp.fontSize = m.fontSize;
                tmp.color = m.color;
                tmp.raycastTarget = m.raycastTarget;
                tmp.richText = m.richText;
                tmp.alignment = MapAlignment(m.alignment);

                if (m.fontStyle == 1)
                    tmp.fontStyle = FontStyles.Bold;
                else if (m.fontStyle == 2)
                    tmp.fontStyle = FontStyles.Italic;
                else if (m.fontStyle == 3)
                    tmp.fontStyle = FontStyles.Bold | FontStyles.Italic;
            }

            // Rewire CardUI or UpgradeCardUI SerializeField refs
            var cardUI = prefabRoot.GetComponent<TowerDefense.UI.CardUI>();
            if (cardUI != null)
            {
                var so = new SerializedObject(cardUI);
                var cooldownTextGO = prefabRoot.transform.Find("CooldownOverlay/CooldownText");
                if (cooldownTextGO != null)
                {
                    var tmpComp = cooldownTextGO.GetComponent<TextMeshProUGUI>();
                    if (tmpComp != null)
                    {
                        var prop = so.FindProperty("cooldownText");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = tmpComp;
                            so.ApplyModifiedProperties();
                            Debug.Log($"Wired CardUI.cooldownText in {path}");
                        }
                    }
                }
            }

            var upgradeUI = prefabRoot.GetComponent<TowerDefense.UI.UpgradeCardUI>();
            if (upgradeUI != null)
            {
                var so = new SerializedObject(upgradeUI);
                TryWirePrefabChild(so, prefabRoot.transform, "nameLabel", "NameLabel");
                TryWirePrefabChild(so, prefabRoot.transform, "descriptionLabel", "DescLabel");
                TryWirePrefabChild(so, prefabRoot.transform, "costLabel", "Bottom/CostLabel");
                TryWirePrefabChild(so, prefabRoot.transform, "buyButtonText", "Bottom/BuyBtn/BuyBtnText");
                so.ApplyModifiedProperties();
                Debug.Log($"Wired UpgradeCardUI fields in {path}");
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.Log($"Migrated {migrations.Count} Text components in prefab: {path}");
        }
    }

    static void TryWirePrefabChild(SerializedObject so, Transform root, string propertyName, string childPath)
    {
        var child = root.Find(childPath);
        if (child == null)
        {
            Debug.LogWarning($"Prefab child not found: {childPath}");
            return;
        }
        var tmp = child.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            Debug.LogWarning($"No TMP on prefab child: {childPath}");
            return;
        }
        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogWarning($"Property not found: {propertyName}");
            return;
        }
        prop.objectReferenceValue = tmp;
    }

    static GameObject FindByPath(string path)
    {
        // Split path and traverse
        var parts = path.Split('/');
        if (parts.Length == 0) return null;

        // Find root
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        GameObject current = null;
        foreach (var root in roots)
        {
            if (root.name == parts[0])
            {
                current = root;
                break;
            }
        }
        if (current == null) return null;

        for (int i = 1; i < parts.Length; i++)
        {
            var child = current.transform.Find(parts[i]);
            if (child == null) return null;
            current = child.gameObject;
        }
        return current;
    }
}
