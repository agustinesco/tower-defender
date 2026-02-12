using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public class RebuildLabPanel : MonoBehaviour
{
    [MenuItem("Tools/Rebuild MainMenu Lab Panel")]
    static void Rebuild()
    {
        // Find SafeArea
        var safeArea = FindByPath("MainMenuCanvas/SafeArea");
        if (safeArea == null)
        {
            Debug.LogError("SafeArea not found!");
            return;
        }

        // Find existing button areas
        var questArea = FindByPath("MainMenuCanvas/SafeArea/QuestArea");
        var startArea = FindByPath("MainMenuCanvas/SafeArea/StartArea");
        var labArea = FindByPath("MainMenuCanvas/SafeArea/LabArea");

        // =====================
        // 1. Create MapPanel
        // =====================
        var mapPanel = new GameObject("MapPanel");
        mapPanel.transform.SetParent(safeArea.transform, false);
        var mapRect = mapPanel.AddComponent<RectTransform>();
        mapRect.anchorMin = Vector2.zero;
        mapRect.anchorMax = Vector2.one;
        mapRect.offsetMin = Vector2.zero;
        mapRect.offsetMax = Vector2.zero;

        // Reparent existing buttons under MapPanel
        if (questArea != null) questArea.transform.SetParent(mapPanel.transform, true);
        if (startArea != null) startArea.transform.SetParent(mapPanel.transform, true);
        if (labArea != null) labArea.transform.SetParent(mapPanel.transform, true);

        // ResourceText at top of MapPanel
        var resTextObj = new GameObject("ResourceText");
        resTextObj.transform.SetParent(mapPanel.transform, false);
        var resTextRect = resTextObj.AddComponent<RectTransform>();
        resTextRect.anchorMin = new Vector2(0, 1);
        resTextRect.anchorMax = new Vector2(1, 1);
        resTextRect.pivot = new Vector2(0.5f, 1);
        resTextRect.anchoredPosition = new Vector2(0, -10);
        resTextRect.sizeDelta = new Vector2(0, 40);
        var resText = resTextObj.AddComponent<TextMeshProUGUI>();
        resText.text = "Iron: 0    Gems: 0    Florpus: 0    Adamantite: 0";
        resText.fontSize = 16;
        resText.color = Color.white;
        resText.alignment = TextAlignmentOptions.Center;
        resText.raycastTarget = false;

        // Move MapPanel to be first sibling (behind QuestPanel)
        mapPanel.transform.SetAsFirstSibling();

        // =====================
        // 2. Create LabPanel
        // =====================
        var labPanel = new GameObject("LabPanel");
        labPanel.transform.SetParent(safeArea.transform, false);
        var labRect = labPanel.AddComponent<RectTransform>();
        labRect.anchorMin = Vector2.zero;
        labRect.anchorMax = Vector2.one;
        labRect.offsetMin = Vector2.zero;
        labRect.offsetMax = Vector2.zero;
        var labBg = labPanel.AddComponent<Image>();
        labBg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        labPanel.SetActive(false); // hidden by default

        // LabResourceLabel at top
        var labResObj = new GameObject("LabResourceLabel");
        labResObj.transform.SetParent(labPanel.transform, false);
        var labResRect = labResObj.AddComponent<RectTransform>();
        labResRect.anchorMin = new Vector2(0, 1);
        labResRect.anchorMax = new Vector2(1, 1);
        labResRect.pivot = new Vector2(0.5f, 1);
        labResRect.anchoredPosition = new Vector2(0, -10);
        labResRect.sizeDelta = new Vector2(0, 40);
        var labResText = labResObj.AddComponent<TextMeshProUGUI>();
        labResText.text = "Iron: 0    Gems: 0    Florpus: 0    Adamantite: 0";
        labResText.fontSize = 16;
        labResText.color = Color.white;
        labResText.alignment = TextAlignmentOptions.Center;
        labResText.raycastTarget = false;

        // Tab Row
        var tabRow = new GameObject("TabRow");
        tabRow.transform.SetParent(labPanel.transform, false);
        var tabRowRect = tabRow.AddComponent<RectTransform>();
        tabRowRect.anchorMin = new Vector2(0, 1);
        tabRowRect.anchorMax = new Vector2(1, 1);
        tabRowRect.pivot = new Vector2(0.5f, 1);
        tabRowRect.anchoredPosition = new Vector2(0, -55);
        tabRowRect.sizeDelta = new Vector2(0, 50);
        var tabHLG = tabRow.AddComponent<HorizontalLayoutGroup>();
        tabHLG.spacing = 4;
        tabHLG.childForceExpandWidth = true;
        tabHLG.childForceExpandHeight = true;
        tabHLG.childControlWidth = true;
        tabHLG.childControlHeight = true;
        tabHLG.padding = new RectOffset(8, 8, 0, 0);

        string[] tabNames = { "Upgrades", "Towers", "Mods" };
        var tabButtons = new Button[3];
        var tabImages = new Image[3];
        var tabTexts = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            var tabObj = new GameObject($"Tab{i}");
            tabObj.transform.SetParent(tabRow.transform, false);
            tabObj.AddComponent<RectTransform>();
            tabImages[i] = tabObj.AddComponent<Image>();
            tabImages[i].color = new Color(0.18f, 0.18f, 0.22f);
            tabButtons[i] = tabObj.AddComponent<Button>();
            tabButtons[i].targetGraphic = tabImages[i];

            var tabTextObj = new GameObject("Text");
            tabTextObj.transform.SetParent(tabObj.transform, false);
            var ttRect = tabTextObj.AddComponent<RectTransform>();
            ttRect.anchorMin = Vector2.zero;
            ttRect.anchorMax = Vector2.one;
            ttRect.offsetMin = Vector2.zero;
            ttRect.offsetMax = Vector2.zero;
            tabTexts[i] = tabTextObj.AddComponent<TextMeshProUGUI>();
            tabTexts[i].text = tabNames[i];
            tabTexts[i].fontSize = 18;
            tabTexts[i].fontStyle = FontStyles.Bold;
            tabTexts[i].color = Color.white;
            tabTexts[i].alignment = TextAlignmentOptions.Center;
        }

        // ScrollView for upgrade cards
        var scrollObj = new GameObject("LabScrollView");
        scrollObj.transform.SetParent(labPanel.transform, false);
        var scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(4, 60);  // bottom margin for back button
        scrollRect.offsetMax = new Vector2(-4, -110); // top margin for resource + tabs
        var scrollView = scrollObj.AddComponent<ScrollRect>();
        scrollView.horizontal = false;
        scrollView.vertical = true;
        var scrollImg = scrollObj.AddComponent<Image>();
        scrollImg.color = new Color(0, 0, 0, 0.01f); // nearly transparent, needed for mask
        scrollView.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        var viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var vpRect = viewportObj.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;
        viewportObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;
        scrollView.viewport = vpRect;

        // Content (GridLayoutGroup)
        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 12); // will grow via ContentSizeFitter
        var grid = contentObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(500, 160);
        grid.spacing = new Vector2(12, 12);
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollView.content = contentRect;

        // Back Button
        var backBtnObj = new GameObject("LabBackBtn");
        backBtnObj.transform.SetParent(labPanel.transform, false);
        var bbRect = backBtnObj.AddComponent<RectTransform>();
        bbRect.anchorMin = new Vector2(0.3f, 0);
        bbRect.anchorMax = new Vector2(0.7f, 0);
        bbRect.pivot = new Vector2(0.5f, 0);
        bbRect.anchoredPosition = new Vector2(0, 10);
        bbRect.sizeDelta = new Vector2(0, 50);
        var bbImg = backBtnObj.AddComponent<Image>();
        bbImg.color = new Color(0.3f, 0.3f, 0.35f);
        var bbBtn = backBtnObj.AddComponent<Button>();
        bbBtn.targetGraphic = bbImg;

        var bbTextObj = new GameObject("Text");
        bbTextObj.transform.SetParent(backBtnObj.transform, false);
        var bbtRect = bbTextObj.AddComponent<RectTransform>();
        bbtRect.anchorMin = Vector2.zero;
        bbtRect.anchorMax = Vector2.one;
        bbtRect.offsetMin = Vector2.zero;
        bbtRect.offsetMax = Vector2.zero;
        var bbText = bbTextObj.AddComponent<TextMeshProUGUI>();
        bbText.text = "Back";
        bbText.fontSize = 20;
        bbText.fontStyle = FontStyles.Bold;
        bbText.color = Color.white;
        bbText.alignment = TextAlignmentOptions.Center;

        // =====================
        // 3. Wire MainSceneController
        // =====================
        var mscArr = Resources.FindObjectsOfTypeAll<TowerDefense.UI.MainSceneController>();
        if (mscArr.Length > 0)
        {
            var so = new SerializedObject(mscArr[0]);

            so.FindProperty("mapPanel").objectReferenceValue = mapPanel;
            so.FindProperty("resourceText").objectReferenceValue = resText;
            so.FindProperty("labPanel").objectReferenceValue = labPanel;
            so.FindProperty("labResourceLabel").objectReferenceValue = labResText;
            so.FindProperty("labGridContent").objectReferenceValue = contentObj.transform;
            so.FindProperty("labBackButton").objectReferenceValue = bbBtn;

            // Tab arrays
            var tabImgProp = so.FindProperty("labTabImages");
            tabImgProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
                tabImgProp.GetArrayElementAtIndex(i).objectReferenceValue = tabImages[i];

            var tabTextProp = so.FindProperty("labTabTexts");
            tabTextProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
                tabTextProp.GetArrayElementAtIndex(i).objectReferenceValue = tabTexts[i];

            var tabBtnProp = so.FindProperty("labTabButtons");
            tabBtnProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
                tabBtnProp.GetArrayElementAtIndex(i).objectReferenceValue = tabButtons[i];

            // Also wire questResourceLabel if not already wired
            var questResGO = FindByPath("MainMenuCanvas/SafeArea/QuestPanel/QuestResources");
            if (questResGO != null)
            {
                var qrTmp = questResGO.GetComponent<TextMeshProUGUI>();
                if (qrTmp != null)
                    so.FindProperty("questResourceLabel").objectReferenceValue = qrTmp;
            }

            so.ApplyModifiedProperties();
            Debug.Log("MainSceneController fully wired!");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Lab panel rebuilt successfully!");
    }

    static GameObject FindByPath(string path)
    {
        var parts = path.Split('/');
        if (parts.Length == 0) return null;

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
