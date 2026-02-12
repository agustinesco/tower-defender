using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class PickedCardsUI : MonoBehaviour
    {
        [SerializeField] private GameObject viewButton;
        [SerializeField] private GameObject overlayPanel;
        [SerializeField] private GameObject cardsContainer;

        private List<GameObject> cardObjects = new List<GameObject>();
        private bool isShowing = false;

        private void Awake()
        {
            // Wire button events
            if (viewButton != null)
            {
                var btn = viewButton.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(ToggleOverlay);
            }
            if (overlayPanel != null)
            {
                var btn = overlayPanel.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(Hide);
            }

            if (overlayPanel != null)
                overlayPanel.SetActive(false);
        }

        private void Start()
        {
            if (UpgradeManager.Instance != null)
            {
                UpgradeManager.Instance.OnUpgradesChanged += UpdateButtonVisibility;
            }
            UpdateButtonVisibility();
        }

        private void OnDestroy()
        {
            if (UpgradeManager.Instance != null)
            {
                UpgradeManager.Instance.OnUpgradesChanged -= UpdateButtonVisibility;
            }
        }

        private void UpdateButtonVisibility()
        {
            if (UpgradeManager.Instance == null) return;

            var levels = UpgradeManager.Instance.GetUpgradeLevels();
            bool hasUpgrades = levels.Count > 0;
            if (viewButton != null)
                viewButton.SetActive(hasUpgrades);
        }

        private void ToggleOverlay()
        {
            if (isShowing)
                Hide();
            else
                Show();
        }

        public void Show()
        {
            if (UpgradeManager.Instance == null) return;

            PopulateCards();
            if (overlayPanel != null)
                overlayPanel.SetActive(true);
            isShowing = true;
        }

        public void Hide()
        {
            if (overlayPanel != null)
                overlayPanel.SetActive(false);
            isShowing = false;
        }

        private void PopulateCards()
        {
            foreach (var cardObj in cardObjects)
            {
                if (cardObj != null) Destroy(cardObj);
            }
            cardObjects.Clear();

            var levels = UpgradeManager.Instance.GetUpgradeLevels();
            if (levels.Count == 0) return;

            float cardWidth = 140f;
            float cardHeight = 200f;
            float spacing = 20f;
            int cardsPerRow = 4;

            int index = 0;
            foreach (var kvp in levels)
            {
                int row = index / cardsPerRow;
                int col = index % cardsPerRow;

                int rowCount = Mathf.Min(levels.Count - row * cardsPerRow, cardsPerRow);
                float totalRowWidth = rowCount * cardWidth + (rowCount - 1) * spacing;
                float startX = -totalRowWidth / 2f + cardWidth / 2f;

                float xPos = startX + col * (cardWidth + spacing);
                float yPos = -row * (cardHeight + spacing);

                CreateCard(kvp.Key, kvp.Value, new Vector2(xPos, yPos), new Vector2(cardWidth, cardHeight));
                index++;
            }
        }

        private void CreateCard(UpgradeCard cardData, int level, Vector2 position, Vector2 size)
        {
            GameObject cardObj = new GameObject($"Card_{cardData.cardName}");
            cardObj.transform.SetParent(cardsContainer.transform);

            var rect = cardObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var bgImage = cardObj.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f);

            // Border
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(cardObj.transform);
            var borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-3, -3);
            borderRect.offsetMax = new Vector2(3, 3);
            borderObj.transform.SetAsFirstSibling();

            var borderImage = borderObj.AddComponent<Image>();
            borderImage.color = new Color(0.4f, 0.6f, 0.8f);

            // Card title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(cardObj.transform);

            var titleRectTransform = titleObj.AddComponent<RectTransform>();
            titleRectTransform.anchorMin = new Vector2(0, 0.3f);
            titleRectTransform.anchorMax = new Vector2(1, 0.5f);
            titleRectTransform.offsetMin = new Vector2(5, 0);
            titleRectTransform.offsetMax = new Vector2(-5, 0);

            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = cardData.cardName;
            titleText.fontSize = 14;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;

            // Level badge
            GameObject badgeObj = new GameObject("LevelBadge");
            badgeObj.transform.SetParent(cardObj.transform);

            var badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1, 1);
            badgeRect.anchorMax = new Vector2(1, 1);
            badgeRect.anchoredPosition = new Vector2(-20, -20);
            badgeRect.sizeDelta = new Vector2(40, 30);

            var badgeImage = badgeObj.AddComponent<Image>();
            badgeImage.color = new Color(0.2f, 0.5f, 0.8f);

            GameObject badgeTextObj = new GameObject("BadgeText");
            badgeTextObj.transform.SetParent(badgeObj.transform);

            var badgeTextRect = badgeTextObj.AddComponent<RectTransform>();
            badgeTextRect.anchorMin = Vector2.zero;
            badgeTextRect.anchorMax = Vector2.one;
            badgeTextRect.offsetMin = Vector2.zero;
            badgeTextRect.offsetMax = Vector2.zero;

            var badgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
            badgeText.text = $"Lv.{level}";
            badgeText.fontSize = 14;
            badgeText.color = Color.white;
            badgeText.alignment = TextAlignmentOptions.Center;

            cardObjects.Add(cardObj);
        }
    }
}
