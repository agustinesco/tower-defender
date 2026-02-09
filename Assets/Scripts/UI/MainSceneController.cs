using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class MainSceneController : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject mapPanel;
        private GameObject labPanel;
        private Text resourceText;
        private List<GameObject> labUpgradeRows = new List<GameObject>();

        private void Awake()
        {
            // Ensure persistent managers exist
            if (PersistenceManager.Instance == null)
            {
                var persistObj = new GameObject("PersistenceManager");
                persistObj.AddComponent<PersistenceManager>();
            }
            if (LabManager.Instance == null)
            {
                var labObj = new GameObject("LabManager");
                labObj.AddComponent<LabManager>();
            }
        }

        private void Start()
        {
            Time.timeScale = 1f;
            CreateCanvas();
            CreateMapPanel();
            CreateLabPanel();
            ShowMap();
        }

        // --- Canvas ---

        private void CreateCanvas()
        {
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // --- Map Panel ---

        private void CreateMapPanel()
        {
            mapPanel = new GameObject("MapPanel");
            mapPanel.transform.SetParent(canvas.transform);

            var rect = mapPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = mapPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 1f);

            // Title
            CreateLabel(mapPanel.transform, "Title", new Vector2(0.5f, 0.88f),
                "Tower Defense", 52, Color.white, new Vector2(500, 70));

            // Resource display
            resourceText = CreateLabel(mapPanel.transform, "Resources", new Vector2(0.5f, 0.78f),
                "", 20, new Color(0.8f, 0.8f, 0.6f), new Vector2(700, 40));

            // Battlefield area panel
            CreateAreaPanel(mapPanel.transform, "BattlefieldArea",
                new Vector2(0.5f, 0.52f), new Vector2(400, 200),
                "Battlefield", "Enter the battlefield and defend your castle",
                new Color(0.15f, 0.30f, 0.15f, 1f), new Color(0.1f, 0.22f, 0.1f, 1f),
                OnEmbarkClicked);

            // Continuous mode area panel
            CreateAreaPanel(mapPanel.transform, "ContinuousArea",
                new Vector2(0.5f, 0.25f), new Vector2(400, 200),
                "Continuous", "Endless waves of increasing difficulty",
                new Color(0.30f, 0.15f, 0.15f, 1f), new Color(0.22f, 0.1f, 0.1f, 1f),
                OnContinuousClicked);

            // Lab area panel
            CreateAreaPanel(mapPanel.transform, "LabArea",
                new Vector2(0.5f, 0.02f), new Vector2(400, 120),
                "Lab", "Research upgrades and unlock new towers",
                new Color(0.15f, 0.15f, 0.30f, 1f), new Color(0.1f, 0.1f, 0.22f, 1f),
                OnLabClicked);
        }

        private void CreateAreaPanel(Transform parent, string name, Vector2 anchor, Vector2 size,
            string title, string description, Color bgColor, Color borderColor,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var bg = panel.AddComponent<Image>();
            bg.color = bgColor;

            var btn = panel.AddComponent<Button>();
            btn.targetGraphic = bg;

            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.3f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            btn.onClick.AddListener(onClick);

            // Border
            var borderObj = new GameObject("Border");
            borderObj.transform.SetParent(panel.transform);
            var borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var borderImage = borderObj.AddComponent<Image>();
            borderImage.color = borderColor;
            borderImage.raycastTarget = false;

            // Move border behind content
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(3, 3);

            // Remove the border image object since we're using Outline instead
            Destroy(borderObj);

            // Title text
            CreateLabel(panel.transform, "Title", new Vector2(0.5f, 0.7f),
                title, 32, Color.white, new Vector2(350, 50));

            // Description text
            var descText = CreateLabel(panel.transform, "Desc", new Vector2(0.5f, 0.35f),
                description, 18, new Color(0.7f, 0.7f, 0.7f), new Vector2(350, 50));
        }

        private void UpdateResources()
        {
            if (resourceText == null || PersistenceManager.Instance == null) return;

            var pm = PersistenceManager.Instance;
            resourceText.text = string.Format(
                "Iron: {0}    Gems: {1}    Florpus: {2}    Adamantite: {3}",
                pm.GetBanked(ResourceType.IronOre),
                pm.GetBanked(ResourceType.Gems),
                pm.GetBanked(ResourceType.Florpus),
                pm.GetBanked(ResourceType.Adamantite)
            );
        }

        private void ShowMap()
        {
            UpdateResources();
            mapPanel.SetActive(true);
            labPanel.SetActive(false);
        }

        private void OnEmbarkClicked()
        {
            GameModeSelection.SelectedMode = GameMode.Waves;
            SceneManager.LoadScene(1);
        }

        private void OnContinuousClicked()
        {
            GameModeSelection.SelectedMode = GameMode.Continuous;
            SceneManager.LoadScene(1);
        }

        private void OnLabClicked()
        {
            mapPanel.SetActive(false);
            ShowLab();
        }

        // --- Lab Panel ---

        private void CreateLabPanel()
        {
            labPanel = new GameObject("LabPanel");
            labPanel.transform.SetParent(canvas.transform);

            var rect = labPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = labPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.1f, 1f);

            // Title
            CreateLabel(labPanel.transform, "LabTitle", new Vector2(0.5f, 0.9f),
                "Lab - Permanent Upgrades", 36, Color.white, new Vector2(500, 60));

            // Back button
            CreateMenuButton(labPanel.transform, "BackBtn", new Vector2(0.5f, 0.08f),
                new Vector2(180, 50), "Back", new Color(0.5f, 0.3f, 0.3f), OnLabBackClicked);

            labPanel.SetActive(false);
        }

        private void ShowLab()
        {
            RefreshLabUpgrades();
            labPanel.SetActive(true);
        }

        private void RefreshLabUpgrades()
        {
            foreach (var row in labUpgradeRows)
            {
                if (row != null) Destroy(row);
            }
            labUpgradeRows.Clear();

            if (LabManager.Instance == null) return;

            var upgrades = LabManager.Instance.Upgrades;
            float rowHeight = 70f;
            float startY = 100f;
            float totalHeight = upgrades.Count * rowHeight;
            float topY = totalHeight / 2f;

            for (int i = 0; i < upgrades.Count; i++)
            {
                var upgrade = upgrades[i];
                float yPos = topY - i * rowHeight;
                var row = CreateLabUpgradeRow(upgrade, yPos);
                labUpgradeRows.Add(row);
            }
        }

        private GameObject CreateLabUpgradeRow(LabUpgrade upgrade, float yPos)
        {
            int level = LabManager.Instance.GetLevel(upgrade);
            bool maxed = level >= upgrade.maxLevel;
            bool canBuy = LabManager.Instance.CanPurchase(upgrade);

            GameObject row = new GameObject($"LabRow_{upgrade.upgradeName}");
            row.transform.SetParent(labPanel.transform);

            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(0, yPos);
            rowRect.sizeDelta = new Vector2(700, 60);

            var rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);

            // Name + description
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(row.transform);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(0.4f, 1);
            nameRect.offsetMin = new Vector2(15, 5);
            nameRect.offsetMax = new Vector2(0, -5);

            var nameText = nameObj.AddComponent<Text>();
            nameText.text = $"{upgrade.upgradeName}\n<size=12>{upgrade.description}</size>";
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 18;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;

            // Level display
            var levelObj = new GameObject("Level");
            levelObj.transform.SetParent(row.transform);
            var levelRect = levelObj.AddComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(0.4f, 0);
            levelRect.anchorMax = new Vector2(0.6f, 1);
            levelRect.offsetMin = Vector2.zero;
            levelRect.offsetMax = Vector2.zero;

            var levelText = levelObj.AddComponent<Text>();
            bool isUnlock = upgrade.upgradeType == LabUpgradeType.TowerUnlock;
            if (isUnlock)
            {
                levelText.text = maxed ? "UNLOCKED" : "LOCKED";
                levelText.color = maxed ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.8f, 0.4f, 0.4f);
            }
            else
            {
                levelText.text = $"Lv {level}/{upgrade.maxLevel}";
                levelText.color = maxed ? new Color(0.4f, 0.8f, 0.4f) : Color.white;
            }
            levelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            levelText.fontSize = 20;
            levelText.alignment = TextAnchor.MiddleCenter;

            // Cost / Buy button
            if (!maxed)
            {
                int cost = upgrade.GetCost(level);

                GameObject buyBtn = new GameObject("BuyBtn");
                buyBtn.transform.SetParent(row.transform);

                var buyRect = buyBtn.AddComponent<RectTransform>();
                buyRect.anchorMin = new Vector2(0.65f, 0.1f);
                buyRect.anchorMax = new Vector2(0.95f, 0.9f);
                buyRect.offsetMin = Vector2.zero;
                buyRect.offsetMax = Vector2.zero;

                var buyImage = buyBtn.AddComponent<Image>();
                buyImage.color = canBuy ? new Color(0.2f, 0.5f, 0.2f) : new Color(0.3f, 0.3f, 0.3f);

                var btn = buyBtn.AddComponent<Button>();
                btn.targetGraphic = buyImage;
                btn.interactable = canBuy;

                var capturedUpgrade = upgrade;
                btn.onClick.AddListener(() => OnBuyUpgrade(capturedUpgrade));

                var buyTextObj = new GameObject("Text");
                buyTextObj.transform.SetParent(buyBtn.transform);
                var buyTextRect = buyTextObj.AddComponent<RectTransform>();
                buyTextRect.anchorMin = Vector2.zero;
                buyTextRect.anchorMax = Vector2.one;
                buyTextRect.offsetMin = Vector2.zero;
                buyTextRect.offsetMax = Vector2.zero;

                var buyText = buyTextObj.AddComponent<Text>();
                buyText.text = $"Buy ({cost} {GetResourceShortName(upgrade.costResource)})";
                buyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                buyText.fontSize = 16;
                buyText.color = canBuy ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                buyText.alignment = TextAnchor.MiddleCenter;
            }
            else
            {
                var maxObj = new GameObject("Maxed");
                maxObj.transform.SetParent(row.transform);
                var maxRect = maxObj.AddComponent<RectTransform>();
                maxRect.anchorMin = new Vector2(0.65f, 0);
                maxRect.anchorMax = new Vector2(0.95f, 1);
                maxRect.offsetMin = Vector2.zero;
                maxRect.offsetMax = Vector2.zero;

                var maxText = maxObj.AddComponent<Text>();
                maxText.text = "MAX";
                maxText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                maxText.fontSize = 22;
                maxText.color = new Color(1f, 0.85f, 0.2f);
                maxText.alignment = TextAnchor.MiddleCenter;
            }

            return row;
        }

        private string GetResourceShortName(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.IronOre: return "Iron";
                case ResourceType.Gems: return "Gems";
                case ResourceType.Florpus: return "Florpus";
                case ResourceType.Adamantite: return "Adam";
                default: return type.ToString();
            }
        }

        private void OnBuyUpgrade(LabUpgrade upgrade)
        {
            if (LabManager.Instance != null)
            {
                LabManager.Instance.Purchase(upgrade);
                RefreshLabUpgrades();
                UpdateResources();
            }
        }

        private void OnLabBackClicked()
        {
            labPanel.SetActive(false);
            ShowMap();
        }

        // --- Helpers ---

        private Text CreateLabel(Transform parent, string name, Vector2 anchor, string content,
            int fontSize, Color color, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var text = obj.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;

            return text;
        }

        private Button CreateMenuButton(Transform parent, string name, Vector2 anchor, Vector2 size,
            string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent);

            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var image = btnObj.AddComponent<Image>();
            image.color = color;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = image;
            btn.onClick.AddListener(onClick);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            return btn;
        }

        // --- Static helper for scene transitions ---

        public static void LoadMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }
    }
}
