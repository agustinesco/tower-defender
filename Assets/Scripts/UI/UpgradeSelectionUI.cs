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

        private void OnEnable()
        {
            if (UpgradeManager.Instance != null)
                UpgradeManager.Instance.OnUpgradesChanged += RefreshCards;
            if (PersistenceManager.Instance != null)
                PersistenceManager.Instance.OnResourcesChanged += RefreshCards;
        }

        private void OnDisable()
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

        private void RefreshCards()
        {
            ClearCards();

            if (UpgradeManager.Instance == null) return;

            var allCards = UpgradeManager.Instance.AllUpgradeCards;
            if (allCards.Count == 0) return;

            foreach (var card in allCards)
            {
                CreateCard(card);
            }
        }

        private void ClearCards()
        {
            foreach (var cardObj in cardObjects)
            {
                if (cardObj != null) Destroy(cardObj);
            }
            cardObjects.Clear();
        }

        private void CreateCard(UpgradeCard cardData)
        {
            var mgr = UpgradeManager.Instance;
            int level = mgr.GetLevel(cardData);
            bool maxed = mgr.IsMaxLevel(cardData);
            int cost = maxed ? 0 : mgr.GetNextCost(cardData);
            bool canAfford = !maxed && PersistenceManager.Instance != null &&
                             PersistenceManager.Instance.GetRunGathered(cardData.costResource) >= cost;

            float rowHeight = 50f;

            // Row root
            GameObject rowObj = new GameObject($"Row_{cardData.cardName}");
            rowObj.transform.SetParent(cardsContainer.transform, false);

            var rowRect = rowObj.AddComponent<LayoutElement>();
            rowRect.preferredHeight = rowHeight;
            rowRect.flexibleWidth = 1;

            var rowRt = rowObj.AddComponent<RectTransform>();

            // Background
            var bgImage = rowObj.AddComponent<Image>();
            Color borderColor = GetResourceColor(cardData.costResource);
            bgImage.color = maxed ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.18f, 0.18f, 0.22f);

            // Left color accent bar
            var accentObj = new GameObject("Accent");
            accentObj.transform.SetParent(rowObj.transform, false);
            var accentRect = accentObj.AddComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0, 0);
            accentRect.anchorMax = new Vector2(0, 1);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(4, 0);
            accentRect.pivot = new Vector2(0, 0.5f);
            var accentImage = accentObj.AddComponent<Image>();
            accentImage.color = borderColor;

            // Description + level (left side)
            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(rowObj.transform, false);
            var descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0);
            descRect.anchorMax = new Vector2(0.50f, 1);
            descRect.offsetMin = new Vector2(12, 2);
            descRect.offsetMax = new Vector2(0, -2);

            var descText = descObj.AddComponent<Text>();
            string levelStr = maxed ? $"(MAX)" : $"Lv.{level}/{cardData.maxLevel}";
            descText.text = $"{cardData.cardName}  <color=#FFE680>{levelStr}</color>\n<color=#CCCCCC><size=12>{cardData.description}</size></color>";
            descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            descText.fontSize = 15;
            descText.color = Color.white;
            descText.alignment = TextAnchor.MiddleLeft;
            descText.supportRichText = true;

            // Resource icon (center-right)
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(rowObj.transform, false);
            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.52f, 0.15f);
            iconRect.anchorMax = new Vector2(0.52f, 0.85f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(24, 0);

            var iconImage = iconObj.AddComponent<Image>();
            iconImage.preserveAspect = true;
            if (GameManager.Instance != null)
            {
                var sprite = GameManager.Instance.GetResourceSprite(cardData.costResource);
                if (sprite != null)
                    iconImage.sprite = sprite;
                iconImage.color = GameManager.Instance.GetResourceColor(cardData.costResource);
            }

            // Resource name (next to icon)
            var resNameObj = new GameObject("ResName");
            resNameObj.transform.SetParent(rowObj.transform, false);
            var resNameRect = resNameObj.AddComponent<RectTransform>();
            resNameRect.anchorMin = new Vector2(0.55f, 0);
            resNameRect.anchorMax = new Vector2(0.72f, 1);
            resNameRect.offsetMin = Vector2.zero;
            resNameRect.offsetMax = Vector2.zero;

            var resNameText = resNameObj.AddComponent<Text>();
            resNameText.text = cardData.costResource.ToString();
            resNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            resNameText.fontSize = 13;
            resNameText.color = GetResourceColor(cardData.costResource);
            resNameText.alignment = TextAnchor.MiddleLeft;

            // Cost text (right side)
            var costObj = new GameObject("Cost");
            costObj.transform.SetParent(rowObj.transform, false);
            var costRect = costObj.AddComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0.73f, 0);
            costRect.anchorMax = new Vector2(0.86f, 1);
            costRect.offsetMin = Vector2.zero;
            costRect.offsetMax = Vector2.zero;

            var costText = costObj.AddComponent<Text>();
            costText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            costText.fontSize = 16;
            costText.alignment = TextAnchor.MiddleCenter;
            if (maxed)
            {
                costText.text = "MAX";
                costText.color = new Color(0.5f, 0.5f, 0.5f);
            }
            else
            {
                costText.text = $"{cost}";
                costText.color = canAfford ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            }

            // Buy button (right edge)
            var buyObj = new GameObject("BuyBtn");
            buyObj.transform.SetParent(rowObj.transform, false);
            var buyRect = buyObj.AddComponent<RectTransform>();
            buyRect.anchorMin = new Vector2(0.87f, 0.1f);
            buyRect.anchorMax = new Vector2(0.99f, 0.9f);
            buyRect.offsetMin = Vector2.zero;
            buyRect.offsetMax = Vector2.zero;

            var buyBg = buyObj.AddComponent<Image>();
            if (maxed)
                buyBg.color = new Color(0.25f, 0.25f, 0.25f);
            else if (canAfford)
                buyBg.color = new Color(0.2f, 0.6f, 0.3f);
            else
                buyBg.color = new Color(0.4f, 0.2f, 0.2f);

            var buyButton = buyObj.AddComponent<Button>();
            buyButton.targetGraphic = buyBg;
            buyButton.interactable = canAfford;

            var data = cardData;
            buyButton.onClick.AddListener(() => OnBuyClicked(data));

            var buyTextObj = new GameObject("Text");
            buyTextObj.transform.SetParent(buyObj.transform, false);
            var buyTextRect = buyTextObj.AddComponent<RectTransform>();
            buyTextRect.anchorMin = Vector2.zero;
            buyTextRect.anchorMax = Vector2.one;
            buyTextRect.offsetMin = Vector2.zero;
            buyTextRect.offsetMax = Vector2.zero;

            var buyText = buyTextObj.AddComponent<Text>();
            buyText.text = maxed ? "-" : "Buy";
            buyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buyText.fontSize = 14;
            buyText.color = Color.white;
            buyText.alignment = TextAnchor.MiddleCenter;

            cardObjects.Add(rowObj);
        }

        private void OnBuyClicked(UpgradeCard card)
        {
            UpgradeManager.Instance?.BuyUpgrade(card);
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
