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
        [Header("Map Panel")]
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private Text resourceText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button labButton;

        [Header("Lab Panel")]
        [SerializeField] private GameObject labPanel;
        [SerializeField] private Text labResourceLabel;
        [SerializeField] private Transform labGridContent;
        [SerializeField] private Button labBackButton;
        [SerializeField] private Image[] labTabImages;
        [SerializeField] private Text[] labTabTexts;
        [SerializeField] private Button[] labTabButtons;

        private int activeLabTab;
        private List<GameObject> labUpgradeCards = new List<GameObject>();

        private static readonly Color[] LabTabActiveColors = {
            new Color(0.25f, 0.3f, 0.55f),
            new Color(0.55f, 0.25f, 0.25f),
            new Color(0.25f, 0.45f, 0.3f)
        };
        private static readonly Color LabTabInactiveColor = new Color(0.18f, 0.18f, 0.22f);

        private void Awake()
        {
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

            if (startButton != null) startButton.onClick.AddListener(OnContinuousClicked);
            if (labButton != null) labButton.onClick.AddListener(OnLabClicked);
            if (labBackButton != null) labBackButton.onClick.AddListener(OnLabBackClicked);

            if (labTabButtons != null)
            {
                for (int i = 0; i < labTabButtons.Length; i++)
                {
                    int tabIndex = i;
                    labTabButtons[i].onClick.AddListener(() => OnLabTabClicked(tabIndex));
                }
            }

            ShowMap();
        }

        // --- Navigation ---

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
            if (mapPanel != null) mapPanel.SetActive(true);
            if (labPanel != null) labPanel.SetActive(false);
        }

        private void OnContinuousClicked()
        {
            GameModeSelection.SelectedMode = GameMode.Continuous;
            SceneManager.LoadScene(1);
        }

        private void OnLabClicked()
        {
            if (mapPanel != null) mapPanel.SetActive(false);
            ShowLab();
        }

        // --- Lab ---

        private void ShowLab()
        {
            activeLabTab = 0;
            UpdateLabResources();
            UpdateLabTabStyles();
            RefreshLabUpgrades();
            if (labPanel != null) labPanel.SetActive(true);
        }

        private void OnLabTabClicked(int tab)
        {
            if (tab == activeLabTab) return;
            activeLabTab = tab;
            UpdateLabTabStyles();
            RefreshLabUpgrades();
        }

        private void UpdateLabTabStyles()
        {
            if (labTabImages == null) return;
            for (int i = 0; i < labTabImages.Length; i++)
            {
                bool active = i == activeLabTab;
                labTabImages[i].color = active ? LabTabActiveColors[i] : LabTabInactiveColor;
                if (labTabTexts != null && i < labTabTexts.Length)
                    labTabTexts[i].color = active ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            }
        }

        private bool UpgradeMatchesTab(LabUpgrade upgrade, int tab)
        {
            switch (tab)
            {
                case 0: return upgrade.upgradeType != LabUpgradeType.TowerUnlock && upgrade.upgradeType != LabUpgradeType.ModUnlock;
                case 1: return upgrade.upgradeType == LabUpgradeType.TowerUnlock;
                case 2: return upgrade.upgradeType == LabUpgradeType.ModUnlock;
                default: return false;
            }
        }

        private void UpdateLabResources()
        {
            if (labResourceLabel == null || PersistenceManager.Instance == null) return;

            var pm = PersistenceManager.Instance;
            labResourceLabel.text = string.Format(
                "Iron: {0}    Gems: {1}    Florpus: {2}    Adamantite: {3}",
                pm.GetBanked(ResourceType.IronOre),
                pm.GetBanked(ResourceType.Gems),
                pm.GetBanked(ResourceType.Florpus),
                pm.GetBanked(ResourceType.Adamantite)
            );
        }

        private void RefreshLabUpgrades()
        {
            foreach (var card in labUpgradeCards)
            {
                if (card != null) Destroy(card);
            }
            labUpgradeCards.Clear();

            if (LabManager.Instance == null || labGridContent == null) return;

            var upgrades = LabManager.Instance.Upgrades;
            for (int i = 0; i < upgrades.Count; i++)
            {
                var upgrade = upgrades[i];
                if (!UpgradeMatchesTab(upgrade, activeLabTab)) continue;
                labUpgradeCards.Add(CreateLabUpgradeCard(upgrade));
            }
        }

        private GameObject CreateLabUpgradeCard(LabUpgrade upgrade)
        {
            int level = LabManager.Instance.GetLevel(upgrade);
            bool maxed = level >= upgrade.maxLevel;
            bool canBuy = LabManager.Instance.CanPurchase(upgrade);

            var card = new GameObject($"Card_{upgrade.upgradeName}");
            card.transform.SetParent(labGridContent);
            card.AddComponent<RectTransform>();

            var cardBg = card.AddComponent<Image>();
            cardBg.color = maxed ? new Color(0.12f, 0.18f, 0.12f, 0.9f) : new Color(0.14f, 0.14f, 0.2f, 0.9f);

            // Left accent bar
            var accentObj = new GameObject("Accent");
            accentObj.transform.SetParent(card.transform);
            var accentRect = accentObj.AddComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0, 0);
            accentRect.anchorMax = new Vector2(0, 1);
            accentRect.pivot = new Vector2(0, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(6, 0);
            var accentImg = accentObj.AddComponent<Image>();
            accentImg.color = GetResourceColor(upgrade.costResource);
            accentImg.raycastTarget = false;

            // Name
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(card.transform);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.65f);
            nameRect.anchorMax = new Vector2(1, 0.95f);
            nameRect.offsetMin = new Vector2(16, 0);
            nameRect.offsetMax = new Vector2(-8, 0);
            var nameText = nameObj.AddComponent<Text>();
            nameText.text = upgrade.upgradeName;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 20;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.raycastTarget = false;

            // Description
            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(card.transform);
            var descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.38f);
            descRect.anchorMax = new Vector2(1, 0.65f);
            descRect.offsetMin = new Vector2(16, 0);
            descRect.offsetMax = new Vector2(-8, 0);
            var descText = descObj.AddComponent<Text>();
            descText.text = upgrade.description;
            descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            descText.fontSize = 14;
            descText.color = new Color(0.7f, 0.7f, 0.7f);
            descText.alignment = TextAnchor.UpperLeft;
            descText.raycastTarget = false;

            // Level / status
            bool isUnlock = upgrade.upgradeType == LabUpgradeType.TowerUnlock || upgrade.upgradeType == LabUpgradeType.ModUnlock;
            var levelObj = new GameObject("Level");
            levelObj.transform.SetParent(card.transform);
            var levelRect = levelObj.AddComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(0, 0.02f);
            levelRect.anchorMax = new Vector2(0.45f, 0.35f);
            levelRect.offsetMin = new Vector2(16, 0);
            levelRect.offsetMax = Vector2.zero;
            var levelText = levelObj.AddComponent<Text>();
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
            levelText.fontSize = 16;
            levelText.alignment = TextAnchor.MiddleLeft;
            levelText.raycastTarget = false;

            // Buy button or MAX label
            if (!maxed)
            {
                int cost = upgrade.GetCost(level);
                var buyBtn = new GameObject("BuyBtn");
                buyBtn.transform.SetParent(card.transform);
                var buyRect = buyBtn.AddComponent<RectTransform>();
                buyRect.anchorMin = new Vector2(0.45f, 0.05f);
                buyRect.anchorMax = new Vector2(0.95f, 0.35f);
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
                buyText.text = $"Buy {cost} {GetResourceShortName(upgrade.costResource)}";
                buyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                buyText.fontSize = 14;
                buyText.color = canBuy ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                buyText.alignment = TextAnchor.MiddleCenter;
            }
            else
            {
                var maxObj = new GameObject("Maxed");
                maxObj.transform.SetParent(card.transform);
                var maxRect = maxObj.AddComponent<RectTransform>();
                maxRect.anchorMin = new Vector2(0.5f, 0.02f);
                maxRect.anchorMax = new Vector2(0.95f, 0.35f);
                maxRect.offsetMin = Vector2.zero;
                maxRect.offsetMax = Vector2.zero;
                var maxText = maxObj.AddComponent<Text>();
                maxText.text = "MAX";
                maxText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                maxText.fontSize = 20;
                maxText.color = new Color(1f, 0.85f, 0.2f);
                maxText.alignment = TextAnchor.MiddleCenter;
                maxText.raycastTarget = false;
            }

            return card;
        }

        private Color GetResourceColor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.IronOre: return new Color(0.6f, 0.6f, 0.7f);
                case ResourceType.Gems: return new Color(0.3f, 0.6f, 0.9f);
                case ResourceType.Florpus: return new Color(0.7f, 0.3f, 0.8f);
                case ResourceType.Adamantite: return new Color(0.9f, 0.7f, 0.2f);
                default: return Color.gray;
            }
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
                UpdateLabResources();
                RefreshLabUpgrades();
                UpdateResources();
            }
        }

        private void OnLabBackClicked()
        {
            if (labPanel != null) labPanel.SetActive(false);
            ShowMap();
        }

        // --- Static helper ---

        public static void LoadMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }
    }
}
