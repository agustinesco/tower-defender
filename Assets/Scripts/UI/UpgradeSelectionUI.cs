using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class UpgradeSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject overlayPanel;
        [SerializeField] private GameObject cardsContainer;
        [SerializeField] private GameObject nextWaveButtonObj;
        [SerializeField] private GameObject exitRunButtonObj;
        [SerializeField] private GameObject closeButtonObj;
        [SerializeField] private Transform tabRow;
        [SerializeField] private Image balanceIcon;
        [SerializeField] private TextMeshProUGUI balanceText;
        [SerializeField] private GameObject upgradeCardPrefab;

        private bool didPause;
        private int activeTab;
        private readonly List<Image> tabImages = new List<Image>();
        private readonly List<TextMeshProUGUI> tabTexts = new List<TextMeshProUGUI>();
        private GridLayoutGroup gridLayout;
        private readonly List<GameObject> cardObjects = new List<GameObject>();

        private static readonly ResourceType[] TabResources = {
            ResourceType.IronOre, ResourceType.Gems, ResourceType.Florpus, ResourceType.Adamantite
        };
        private static readonly Color[] TabActiveColors = {
            new Color(0.45f, 0.45f, 0.55f),
            new Color(0.2f, 0.4f, 0.7f),
            new Color(0.55f, 0.2f, 0.65f),
            new Color(0.7f, 0.55f, 0.15f)
        };
        private static readonly Color TabInactiveColor = new Color(0.18f, 0.18f, 0.22f);

        public event System.Action OnNextWave;
        public event System.Action OnExitRun;

        public bool IsVisible => overlayPanel != null && overlayPanel.activeSelf;

        private void Awake()
        {
            WireButton(nextWaveButtonObj, OnNextWaveClicked);
            WireButton(exitRunButtonObj, OnExitRunClicked);
            WireButton(closeButtonObj, OnCloseClicked);

            if (cardsContainer != null)
                gridLayout = cardsContainer.GetComponent<GridLayoutGroup>();

            WireTabs();
            Hide();
        }

        private void WireTabs()
        {
            if (tabRow == null) return;

            for (int i = 0; i < tabRow.childCount; i++)
            {
                int tabIndex = i;
                var child = tabRow.GetChild(i);
                var img = child.GetComponent<Image>();
                var btn = child.GetComponent<Button>();
                var tmp = child.GetComponentInChildren<TextMeshProUGUI>();

                if (img != null) tabImages.Add(img);
                if (tmp != null) tabTexts.Add(tmp);
                if (btn != null) btn.onClick.AddListener(() => OnTabClicked(tabIndex));
            }
        }

        private void OnTabClicked(int tab)
        {
            if (tab == activeTab) return;
            activeTab = tab;
            UpdateTabStyles();
            RefreshCards();
        }

        private void UpdateTabStyles()
        {
            for (int i = 0; i < tabImages.Count; i++)
            {
                bool active = i == activeTab;
                tabImages[i].color = active ? TabActiveColors[i] : TabInactiveColor;
                if (i < tabTexts.Count)
                    tabTexts[i].color = active ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            }
        }

        private void UpdateBalanceDisplay()
        {
            ResourceType resource = TabResources[activeTab];
            int balance = PersistenceManager.Instance != null
                ? PersistenceManager.Instance.GetRunGathered(resource) : 0;
            if (balanceText != null)
                balanceText.text = $"{balance}";
            if (balanceIcon != null && GameManager.Instance != null)
            {
                var sprite = GameManager.Instance.GetResourceSprite(resource);
                if (sprite != null) balanceIcon.sprite = sprite;
                balanceIcon.color = GameManager.Instance.GetResourceColor(resource);
            }
        }

        private void Start()
        {
            if (UpgradeManager.Instance != null)
                UpgradeManager.Instance.OnUpgradesChanged += RefreshCards;
            if (PersistenceManager.Instance != null)
                PersistenceManager.Instance.OnResourcesChanged += RefreshCards;
        }

        private void OnDestroy()
        {
            if (UpgradeManager.Instance != null)
                UpgradeManager.Instance.OnUpgradesChanged -= RefreshCards;
            if (PersistenceManager.Instance != null)
                PersistenceManager.Instance.OnResourcesChanged -= RefreshCards;
        }

        private static void WireButton(GameObject obj, UnityEngine.Events.UnityAction action)
        {
            if (obj == null) return;
            var btn = obj.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(action);
        }

        private void OnCloseClicked() => Hide();

        private void OnNextWaveClicked()
        {
            Hide();
            OnNextWave?.Invoke();
        }

        private void OnExitRunClicked()
        {
            Hide();
            OnExitRun?.Invoke();
        }

        public void Show()
        {
            didPause = true;
            activeTab = 0;
            UpdateTabStyles();
            RefreshCards();
            overlayPanel.SetActive(true);
            Time.timeScale = 0f;

            if (nextWaveButtonObj != null) nextWaveButtonObj.SetActive(true);
            if (exitRunButtonObj != null) exitRunButtonObj.SetActive(true);
            if (closeButtonObj != null) closeButtonObj.SetActive(false);
        }

        public void ShowWithoutPause()
        {
            didPause = false;
            activeTab = 0;
            UpdateTabStyles();
            RefreshCards();
            overlayPanel.SetActive(true);

            if (nextWaveButtonObj != null) nextWaveButtonObj.SetActive(false);
            if (exitRunButtonObj != null) exitRunButtonObj.SetActive(false);
            if (closeButtonObj != null) closeButtonObj.SetActive(true);
        }

        public void Hide()
        {
            if (overlayPanel != null)
                overlayPanel.SetActive(false);
            if (didPause)
                Time.timeScale = 1f;
        }

        private void RefreshCards()
        {
            if (UpgradeManager.Instance == null || upgradeCardPrefab == null) return;

            ClearCards();
            UpdateGridCellSize();
            UpdateBalanceDisplay();

            var allCards = UpgradeManager.Instance.AllUpgradeCards;
            ResourceType activeResource = TabResources[activeTab];

            foreach (var card in allCards)
            {
                if (card.costResource != activeResource) continue;

                var go = Instantiate(upgradeCardPrefab, cardsContainer.transform);
                cardObjects.Add(go);
                var ui = go.GetComponent<UpgradeCardUI>();
                if (ui != null)
                    PopulateCard(ui, card);
            }
        }

        private void ClearCards()
        {
            foreach (var go in cardObjects)
                Destroy(go);
            cardObjects.Clear();
        }

        private void UpdateGridCellSize()
        {
            if (gridLayout == null || cardsContainer == null) return;

            var containerRT = cardsContainer.GetComponent<RectTransform>();
            var scrollViewRT = containerRT.parent?.parent as RectTransform;
            if (scrollViewRT != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollViewRT);

            float availWidth = containerRT.rect.width;
            if (availWidth <= 0)
            {
                var overlayRT = overlayPanel.GetComponent<RectTransform>();
                availWidth = overlayRT.rect.width * 0.96f;
            }

            float cellW = (availWidth - gridLayout.padding.left - gridLayout.padding.right - gridLayout.spacing.x) / 2f;
            gridLayout.cellSize = new Vector2(cellW, 360f);
        }

        private void PopulateCard(UpgradeCardUI ui, UpgradeCard cardData)
        {
            var mgr = UpgradeManager.Instance;
            int level = mgr.GetLevel(cardData);
            bool maxed = mgr.IsMaxLevel(cardData);
            int cost = maxed ? 0 : mgr.GetNextCost(cardData);
            bool canAfford = !maxed && PersistenceManager.Instance != null &&
                             PersistenceManager.Instance.GetRunGathered(cardData.costResource) >= cost;

            ui.Background.color = maxed ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.18f, 0.18f, 0.22f);

            string levelStr;
            if (maxed) levelStr = "(MAX)";
            else if (cardData.maxLevel <= 0) levelStr = $"Lv.{level}";
            else levelStr = $"Lv.{level}/{cardData.maxLevel}";
            ui.NameLabel.text = $"{cardData.cardName} <color=#FFE680>{levelStr}</color>";

            ui.DescriptionLabel.text = cardData.description;

            if (maxed)
            {
                ui.CostLabel.text = "MAX";
                ui.CostLabel.color = new Color(0.5f, 0.5f, 0.5f);
            }
            else
            {
                ui.CostLabel.text = $"{cost}";
                ui.CostLabel.color = canAfford ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            }

            if (maxed)
                ui.BuyButtonBg.color = new Color(0.25f, 0.25f, 0.25f);
            else if (canAfford)
                ui.BuyButtonBg.color = new Color(0.2f, 0.6f, 0.3f);
            else
                ui.BuyButtonBg.color = new Color(0.4f, 0.2f, 0.2f);

            ui.BuyButton.interactable = canAfford;
            ui.BuyButtonText.text = maxed ? "-" : "Buy";

            ui.BuyButton.onClick.RemoveAllListeners();
            var data = cardData;
            ui.BuyButton.onClick.AddListener(() => OnBuyClicked(data));
        }

        private void OnBuyClicked(UpgradeCard card)
        {
            if (UpgradeManager.Instance != null && UpgradeManager.Instance.BuyUpgrade(card))
                RefreshCards();
        }
    }
}
