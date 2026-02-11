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
        [SerializeField] private GameObject upgradeCardPrefab;

        private List<GameObject> cardObjects = new List<GameObject>();
        private bool didPause;

        public event System.Action OnNextWave;
        public event System.Action OnExitRun;

        public bool IsVisible => overlayPanel != null && overlayPanel.activeSelf;

        private void Awake()
        {
            // Wire button events
            WireButton(nextWaveButtonObj, OnNextWaveClicked);
            WireButton(exitRunButtonObj, OnExitRunClicked);
            WireButton(closeButtonObj, OnCloseClicked);

            Hide();
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

        private void OnCloseClicked()
        {
            Hide();
        }

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
            RefreshCards();
            overlayPanel.SetActive(true);
            Time.timeScale = 0f;

            // Show wave-end buttons, hide close button
            if (nextWaveButtonObj != null) nextWaveButtonObj.SetActive(true);
            if (exitRunButtonObj != null) exitRunButtonObj.SetActive(true);
            if (closeButtonObj != null) closeButtonObj.SetActive(false);
        }

        public void ShowWithoutPause()
        {
            didPause = false;
            RefreshCards();
            overlayPanel.SetActive(true);

            // Hide wave-end buttons, show close button
            if (nextWaveButtonObj != null) nextWaveButtonObj.SetActive(false);
            if (exitRunButtonObj != null) exitRunButtonObj.SetActive(false);
            if (closeButtonObj != null) closeButtonObj.SetActive(true);
        }

        public void Hide()
        {
            if (overlayPanel != null)
                overlayPanel.SetActive(false);
            if (didPause)
            {
                Time.timeScale = 1f;
            }
        }

        private readonly List<ResourceType> _groupOrder = new List<ResourceType>();
        private readonly Dictionary<ResourceType, List<UpgradeCard>> _groups = new Dictionary<ResourceType, List<UpgradeCard>>();

        private void RefreshCards()
        {
            ClearCards();

            if (UpgradeManager.Instance == null) return;

            var allCards = UpgradeManager.Instance.AllUpgradeCards;
            if (allCards.Count == 0) return;

            // Group cards by resource type, preserving insertion order
            _groupOrder.Clear();
            _groups.Clear();
            foreach (var card in allCards)
            {
                if (!_groups.TryGetValue(card.costResource, out var list))
                {
                    list = new List<UpgradeCard>();
                    _groups[card.costResource] = list;
                    _groupOrder.Add(card.costResource);
                }
                list.Add(card);
            }

            foreach (var res in _groupOrder)
                CreateResourceSection(res, _groups[res]);
        }

        private void ClearCards()
        {
            foreach (var cardObj in cardObjects)
            {
                if (cardObj != null) Destroy(cardObj);
            }
            cardObjects.Clear();
        }

        private void CreateResourceSection(ResourceType resource, List<UpgradeCard> cards)
        {
            Color resColor = GetResourceColor(resource);

            // Section root
            var sectionObj = new GameObject($"Section_{resource}", typeof(RectTransform));
            sectionObj.transform.SetParent(cardsContainer.transform, false);
            var sectionLE = sectionObj.AddComponent<LayoutElement>();
            sectionLE.flexibleWidth = 1;

            var sectionVLG = sectionObj.AddComponent<VerticalLayoutGroup>();
            sectionVLG.spacing = 4;
            sectionVLG.padding = new RectOffset(0, 0, 0, 4);
            sectionVLG.childForceExpandWidth = true;
            sectionVLG.childForceExpandHeight = false;
            sectionVLG.childControlWidth = true;
            sectionVLG.childControlHeight = true;

            // --- Header ---
            var headerObj = new GameObject("Header", typeof(RectTransform));
            headerObj.transform.SetParent(sectionObj.transform, false);
            var headerLE = headerObj.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;
            headerLE.flexibleWidth = 1;

            var headerBg = headerObj.AddComponent<Image>();
            headerBg.color = new Color(resColor.r * 0.3f, resColor.g * 0.3f, resColor.b * 0.3f, 0.8f);

            var headerHLG = headerObj.AddComponent<HorizontalLayoutGroup>();
            headerHLG.spacing = 6;
            headerHLG.padding = new RectOffset(8, 8, 2, 2);
            headerHLG.childAlignment = TextAnchor.MiddleLeft;
            headerHLG.childForceExpandWidth = false;
            headerHLG.childForceExpandHeight = false;
            headerHLG.childControlWidth = true;
            headerHLG.childControlHeight = true;

            // Resource icon
            var hIconObj = new GameObject("Icon", typeof(RectTransform));
            hIconObj.transform.SetParent(headerObj.transform, false);
            var hIconLE = hIconObj.AddComponent<LayoutElement>();
            hIconLE.preferredWidth = 20;
            hIconLE.preferredHeight = 20;
            var hIconImg = hIconObj.AddComponent<Image>();
            hIconImg.preserveAspect = true;
            if (GameManager.Instance != null)
            {
                var sprite = GameManager.Instance.GetResourceSprite(resource);
                if (sprite != null) hIconImg.sprite = sprite;
                hIconImg.color = GameManager.Instance.GetResourceColor(resource);
            }

            // Resource name
            var hNameObj = new GameObject("Name", typeof(RectTransform));
            hNameObj.transform.SetParent(headerObj.transform, false);
            var hNameLE = hNameObj.AddComponent<LayoutElement>();
            hNameLE.flexibleWidth = 1;
            var hNameText = hNameObj.AddComponent<Text>();
            hNameText.text = resource.ToString();
            hNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hNameText.fontSize = 14;
            hNameText.fontStyle = FontStyle.Bold;
            hNameText.color = resColor;
            hNameText.alignment = TextAnchor.MiddleLeft;

            // Balance
            var hBalObj = new GameObject("Balance", typeof(RectTransform));
            hBalObj.transform.SetParent(headerObj.transform, false);
            var hBalLE = hBalObj.AddComponent<LayoutElement>();
            hBalLE.preferredWidth = 60;
            var hBalText = hBalObj.AddComponent<Text>();
            int balance = PersistenceManager.Instance != null ? PersistenceManager.Instance.GetRunGathered(resource) : 0;
            hBalText.text = $"{balance}";
            hBalText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hBalText.fontSize = 14;
            hBalText.color = Color.white;
            hBalText.alignment = TextAnchor.MiddleRight;

            // --- Cards Row ---
            var rowObj = new GameObject("CardsRow", typeof(RectTransform));
            rowObj.transform.SetParent(sectionObj.transform, false);

            var rowHLG = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowHLG.spacing = 6;
            rowHLG.childForceExpandWidth = true;
            rowHLG.childForceExpandHeight = false;
            rowHLG.childControlWidth = true;
            rowHLG.childControlHeight = true;

            foreach (var card in cards)
                CreateCardTile(rowObj, card);

            cardObjects.Add(sectionObj);
        }

        private void CreateCardTile(GameObject parent, UpgradeCard cardData)
        {
            var mgr = UpgradeManager.Instance;
            int level = mgr.GetLevel(cardData);
            bool maxed = mgr.IsMaxLevel(cardData);
            int cost = maxed ? 0 : mgr.GetNextCost(cardData);
            bool canAfford = !maxed && PersistenceManager.Instance != null &&
                             PersistenceManager.Instance.GetRunGathered(cardData.costResource) >= cost;

            var tileObj = Instantiate(upgradeCardPrefab, parent.transform);
            tileObj.name = $"Tile_{cardData.cardName}";
            var ui = tileObj.GetComponent<UpgradeCardUI>();

            // Background
            ui.Background.color = maxed ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.18f, 0.18f, 0.22f);

            // Name + Level
            string levelStr;
            if (maxed) levelStr = "(MAX)";
            else if (cardData.maxLevel <= 0) levelStr = $"Lv.{level}";
            else levelStr = $"Lv.{level}/{cardData.maxLevel}";
            ui.NameLabel.text = $"{cardData.cardName} <color=#FFE680>{levelStr}</color>";

            // Description
            ui.DescriptionLabel.text = cardData.description;

            // Cost
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

            // Buy button
            if (maxed)
                ui.BuyButtonBg.color = new Color(0.25f, 0.25f, 0.25f);
            else if (canAfford)
                ui.BuyButtonBg.color = new Color(0.2f, 0.6f, 0.3f);
            else
                ui.BuyButtonBg.color = new Color(0.4f, 0.2f, 0.2f);

            ui.BuyButton.interactable = canAfford;
            ui.BuyButtonText.text = maxed ? "-" : "Buy";

            var data = cardData;
            ui.BuyButton.onClick.AddListener(() => OnBuyClicked(data));
        }

        private void OnBuyClicked(UpgradeCard card)
        {
            if (UpgradeManager.Instance != null && UpgradeManager.Instance.BuyUpgrade(card))
                RefreshCards();
        }

        private Color GetResourceColor(ResourceType type)
        {
            return type switch
            {
                ResourceType.IronOre => new Color(0.6f, 0.6f, 0.65f),
                ResourceType.Gems => new Color(0.4f, 0.2f, 0.8f),
                ResourceType.Florpus => new Color(0.2f, 0.8f, 0.5f),
                ResourceType.Adamantite => new Color(0.85f, 0.3f, 0.3f),
                _ => Color.white
            };
        }
    }
}
