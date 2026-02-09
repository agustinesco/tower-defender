# Upgrade System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement a roguelike-style upgrade card system that shows 3 weighted-random cards after each wave, with effects that stack additively for the current run.

**Architecture:** UpgradeManager singleton tracks bonuses and picked cards. UpgradeCard ScriptableObjects define card data. UpgradeSelectionUI handles the card selection overlay. Tower and Enemy scripts read from UpgradeManager when calculating stats.

**Tech Stack:** Unity C#, ScriptableObjects, Unity UI (programmatic)

---

### Task 1: Create UpgradeCard ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/UpgradeCard.cs`

**Step 1: Create the UpgradeCard ScriptableObject class**

```csharp
using UnityEngine;

namespace TowerDefense.Data
{
    public enum CardRarity { Common, Rare, Epic }
    public enum UpgradeEffectType { TowerSpeed, TowerDamage, EnemySpeedAndGold, ExtraProjectiles }
    public enum IconShape { Circle, Diamond, Star, Hexagon }

    [CreateAssetMenu(fileName = "NewUpgrade", menuName = "Tower Defense/Upgrade Card")]
    public class UpgradeCard : ScriptableObject
    {
        public string cardName;
        [TextArea(2, 4)]
        public string description;
        public CardRarity rarity;
        public UpgradeEffectType effectType;
        public float effectValue;
        public float secondaryValue; // For dual effects like EnemySpeedAndGold
        public IconShape iconShape;

        public Color GetRarityColor()
        {
            return rarity switch
            {
                CardRarity.Common => new Color(0.5f, 0.5f, 0.5f), // Gray
                CardRarity.Rare => new Color(0.255f, 0.412f, 0.882f), // Blue
                CardRarity.Epic => new Color(0.6f, 0.196f, 0.8f), // Purple
                _ => Color.white
            };
        }

        public int GetRarityWeight()
        {
            return rarity switch
            {
                CardRarity.Common => 60,
                CardRarity.Rare => 30,
                CardRarity.Epic => 10,
                _ => 0
            };
        }
    }
}
```

**Step 2: Verify script compiles**

Check Unity console for compilation errors.

---

### Task 2: Create UpgradeManager Singleton

**Files:**
- Create: `Assets/Scripts/Core/UpgradeManager.cs`

**Step 1: Create the UpgradeManager class**

```csharp
using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Data;

namespace TowerDefense.Core
{
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager Instance { get; private set; }

        [SerializeField] private List<UpgradeCard> allUpgradeCards = new List<UpgradeCard>();

        // Active bonuses for current run
        private float towerSpeedBonus = 0f;
        private float towerDamageBonus = 0f;
        private float enemySpeedBonus = 0f;
        private float enemyGoldBonus = 0f;
        private int extraProjectiles = 0;

        private List<UpgradeCard> pickedCards = new List<UpgradeCard>();

        // Public accessors
        public float TowerSpeedBonus => towerSpeedBonus;
        public float TowerDamageBonus => towerDamageBonus;
        public float EnemySpeedBonus => enemySpeedBonus;
        public float EnemyGoldBonus => enemyGoldBonus;
        public int ExtraProjectiles => extraProjectiles;
        public IReadOnlyList<UpgradeCard> PickedCards => pickedCards;
        public IReadOnlyList<UpgradeCard> AllUpgradeCards => allUpgradeCards;

        public event System.Action OnUpgradesChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void ApplyUpgrade(UpgradeCard card)
        {
            pickedCards.Add(card);

            switch (card.effectType)
            {
                case UpgradeEffectType.TowerSpeed:
                    towerSpeedBonus += card.effectValue;
                    break;
                case UpgradeEffectType.TowerDamage:
                    towerDamageBonus += card.effectValue;
                    break;
                case UpgradeEffectType.EnemySpeedAndGold:
                    enemySpeedBonus += card.effectValue;
                    enemyGoldBonus += card.secondaryValue;
                    break;
                case UpgradeEffectType.ExtraProjectiles:
                    extraProjectiles += (int)card.effectValue;
                    break;
            }

            OnUpgradesChanged?.Invoke();
            Debug.Log($"Applied upgrade: {card.cardName}. Speed bonus: {towerSpeedBonus}, Damage bonus: {towerDamageBonus}, Extra projectiles: {extraProjectiles}");
        }

        public List<UpgradeCard> GetRandomCards(int count)
        {
            if (allUpgradeCards.Count == 0)
            {
                Debug.LogWarning("No upgrade cards configured!");
                return new List<UpgradeCard>();
            }

            // Build weighted list
            List<(UpgradeCard card, int weight)> weightedCards = new List<(UpgradeCard, int)>();
            int totalWeight = 0;

            foreach (var card in allUpgradeCards)
            {
                int weight = card.GetRarityWeight();
                weightedCards.Add((card, weight));
                totalWeight += weight;
            }

            // Select random cards
            List<UpgradeCard> selected = new List<UpgradeCard>();
            for (int i = 0; i < count && weightedCards.Count > 0; i++)
            {
                int roll = Random.Range(0, totalWeight);
                int cumulative = 0;

                for (int j = 0; j < weightedCards.Count; j++)
                {
                    cumulative += weightedCards[j].weight;
                    if (roll < cumulative)
                    {
                        selected.Add(weightedCards[j].card);
                        // Allow duplicates in offering, so don't remove
                        break;
                    }
                }
            }

            return selected;
        }

        public void ResetForNewRun()
        {
            towerSpeedBonus = 0f;
            towerDamageBonus = 0f;
            enemySpeedBonus = 0f;
            enemyGoldBonus = 0f;
            extraProjectiles = 0;
            pickedCards.Clear();
            OnUpgradesChanged?.Invoke();
        }

        public Dictionary<UpgradeCard, int> GetPickedCardCounts()
        {
            Dictionary<UpgradeCard, int> counts = new Dictionary<UpgradeCard, int>();
            foreach (var card in pickedCards)
            {
                if (counts.ContainsKey(card))
                    counts[card]++;
                else
                    counts[card] = 1;
            }
            return counts;
        }
    }
}
```

**Step 2: Verify script compiles**

Check Unity console for compilation errors.

---

### Task 3: Create UpgradeSelectionUI

**Files:**
- Create: `Assets/Scripts/UI/UpgradeSelectionUI.cs`

**Step 1: Create the UpgradeSelectionUI class**

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class UpgradeSelectionUI : MonoBehaviour
    {
        private GameObject overlayPanel;
        private GameObject cardsContainer;
        private List<GameObject> cardObjects = new List<GameObject>();
        private Canvas canvas;

        public event System.Action OnCardSelected;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }
            CreateUI();
            Hide();
        }

        private void CreateUI()
        {
            // Dark overlay covering entire screen
            overlayPanel = new GameObject("UpgradeOverlay");
            overlayPanel.transform.SetParent(canvas.transform);

            var overlayRect = overlayPanel.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            var overlayImage = overlayPanel.AddComponent<Image>();
            overlayImage.color = new Color(0, 0, 0, 0.7f);

            // Title
            CreateTitle();

            // Cards container
            cardsContainer = new GameObject("CardsContainer");
            cardsContainer.transform.SetParent(overlayPanel.transform);

            var containerRect = cardsContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = new Vector2(700, 300);
        }

        private void CreateTitle()
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(overlayPanel.transform);

            var rect = titleObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -80);
            rect.sizeDelta = new Vector2(400, 60);

            var text = titleObj.AddComponent<Text>();
            text.text = "Choose an Upgrade";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 36;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
        }

        public void Show(List<UpgradeCard> cards)
        {
            ClearCards();

            float cardWidth = 180f;
            float cardHeight = 260f;
            float spacing = 30f;
            float totalWidth = cards.Count * cardWidth + (cards.Count - 1) * spacing;
            float startX = -totalWidth / 2f + cardWidth / 2f;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                float xPos = startX + i * (cardWidth + spacing);
                CreateCard(card, new Vector2(xPos, 0), new Vector2(cardWidth, cardHeight));
            }

            overlayPanel.SetActive(true);
            Time.timeScale = 0f; // Pause game
        }

        public void Hide()
        {
            overlayPanel.SetActive(false);
            Time.timeScale = 1f; // Resume game
        }

        private void ClearCards()
        {
            foreach (var cardObj in cardObjects)
            {
                if (cardObj != null) Destroy(cardObj);
            }
            cardObjects.Clear();
        }

        private void CreateCard(UpgradeCard cardData, Vector2 position, Vector2 size)
        {
            GameObject cardObj = new GameObject($"Card_{cardData.cardName}");
            cardObj.transform.SetParent(cardsContainer.transform);

            var rect = cardObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            // Card background
            var bgImage = cardObj.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f);

            // Rarity border
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(cardObj.transform);
            var borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-4, -4);
            borderRect.offsetMax = new Vector2(4, 4);
            borderObj.transform.SetAsFirstSibling();

            var borderImage = borderObj.AddComponent<Image>();
            borderImage.color = cardData.GetRarityColor();

            // Icon placeholder
            CreateIconPlaceholder(cardObj.transform, cardData);

            // Card title
            CreateCardTitle(cardObj.transform, cardData.cardName);

            // Card description
            CreateCardDescription(cardObj.transform, cardData.description);

            // Button functionality
            var button = cardObj.AddComponent<Button>();
            button.targetGraphic = bgImage;

            var data = cardData; // Capture for closure
            button.onClick.AddListener(() => OnCardClicked(data));

            cardObjects.Add(cardObj);
        }

        private void CreateIconPlaceholder(Transform parent, UpgradeCard cardData)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(parent);

            var rect = iconObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -70);
            rect.sizeDelta = new Vector2(80, 80);

            var image = iconObj.AddComponent<Image>();
            image.color = cardData.GetRarityColor();

            // Create shape based on icon type
            // For now, all shapes use the same square - could be enhanced later
        }

        private void CreateCardTitle(Transform parent, string title)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(parent);

            var rect = titleObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.anchoredPosition = new Vector2(0, -20);
            rect.sizeDelta = new Vector2(0, 30);

            var text = titleObj.AddComponent<Text>();
            text.text = title;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
        }

        private void CreateCardDescription(Transform parent, string description)
        {
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(parent);

            var rect = descObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0.4f);
            rect.offsetMin = new Vector2(10, 10);
            rect.offsetMax = new Vector2(-10, -10);

            var text = descObj.AddComponent<Text>();
            text.text = description;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = new Color(0.8f, 0.8f, 0.8f);
            text.alignment = TextAnchor.UpperCenter;
        }

        private void OnCardClicked(UpgradeCard card)
        {
            UpgradeManager.Instance?.ApplyUpgrade(card);
            Hide();
            OnCardSelected?.Invoke();
        }
    }
}
```

**Step 2: Verify script compiles**

Check Unity console for compilation errors.

---

### Task 4: Create PickedCardsUI

**Files:**
- Create: `Assets/Scripts/UI/PickedCardsUI.cs`

**Step 1: Create the PickedCardsUI class**

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class PickedCardsUI : MonoBehaviour
    {
        private GameObject viewButton;
        private GameObject overlayPanel;
        private GameObject cardsContainer;
        private List<GameObject> cardObjects = new List<GameObject>();
        private Canvas canvas;
        private bool isShowing = false;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }
            CreateUI();
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

        private void CreateUI()
        {
            CreateViewButton();
            CreateOverlay();
            overlayPanel.SetActive(false);
        }

        private void CreateViewButton()
        {
            viewButton = new GameObject("ViewCardsButton");
            viewButton.transform.SetParent(canvas.transform);

            var rect = viewButton.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(60, 60);
            rect.sizeDelta = new Vector2(50, 50);

            var image = viewButton.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.5f);

            var button = viewButton.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(ToggleOverlay);

            // Button icon (simple "+" or card symbol)
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(viewButton.transform);

            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            var iconText = iconObj.AddComponent<Text>();
            iconText.text = "+";
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 30;
            iconText.color = Color.white;
            iconText.alignment = TextAnchor.MiddleCenter;

            viewButton.SetActive(false); // Hidden until first card picked
        }

        private void CreateOverlay()
        {
            overlayPanel = new GameObject("PickedCardsOverlay");
            overlayPanel.transform.SetParent(canvas.transform);

            var overlayRect = overlayPanel.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            // Click anywhere to close
            var overlayImage = overlayPanel.AddComponent<Image>();
            overlayImage.color = new Color(0, 0, 0, 0.7f);

            var overlayButton = overlayPanel.AddComponent<Button>();
            overlayButton.targetGraphic = overlayImage;
            overlayButton.onClick.AddListener(Hide);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(overlayPanel.transform);

            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -80);
            titleRect.sizeDelta = new Vector2(400, 60);

            var titleText = titleObj.AddComponent<Text>();
            titleText.text = "Your Upgrades";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 36;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleCenter;

            // Cards container
            cardsContainer = new GameObject("CardsContainer");
            cardsContainer.transform.SetParent(overlayPanel.transform);

            var containerRect = cardsContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = new Vector2(800, 400);
        }

        private void UpdateButtonVisibility()
        {
            if (UpgradeManager.Instance == null) return;

            bool hasCards = UpgradeManager.Instance.PickedCards.Count > 0;
            viewButton.SetActive(hasCards);
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
            overlayPanel.SetActive(true);
            isShowing = true;
        }

        public void Hide()
        {
            overlayPanel.SetActive(false);
            isShowing = false;
        }

        private void PopulateCards()
        {
            // Clear existing cards
            foreach (var cardObj in cardObjects)
            {
                if (cardObj != null) Destroy(cardObj);
            }
            cardObjects.Clear();

            var cardCounts = UpgradeManager.Instance.GetPickedCardCounts();
            if (cardCounts.Count == 0) return;

            float cardWidth = 140f;
            float cardHeight = 200f;
            float spacing = 20f;
            int cardsPerRow = 4;

            int index = 0;
            foreach (var kvp in cardCounts)
            {
                int row = index / cardsPerRow;
                int col = index % cardsPerRow;

                float totalRowWidth = Mathf.Min(cardCounts.Count - row * cardsPerRow, cardsPerRow) * cardWidth +
                                     (Mathf.Min(cardCounts.Count - row * cardsPerRow, cardsPerRow) - 1) * spacing;
                float startX = -totalRowWidth / 2f + cardWidth / 2f;

                float xPos = startX + col * (cardWidth + spacing);
                float yPos = -row * (cardHeight + spacing);

                CreateCard(kvp.Key, kvp.Value, new Vector2(xPos, yPos), new Vector2(cardWidth, cardHeight));
                index++;
            }
        }

        private void CreateCard(UpgradeCard cardData, int count, Vector2 position, Vector2 size)
        {
            GameObject cardObj = new GameObject($"Card_{cardData.cardName}");
            cardObj.transform.SetParent(cardsContainer.transform);

            var rect = cardObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            // Card background
            var bgImage = cardObj.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f);

            // Rarity border
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(cardObj.transform);
            var borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-3, -3);
            borderRect.offsetMax = new Vector2(3, 3);
            borderObj.transform.SetAsFirstSibling();

            var borderImage = borderObj.AddComponent<Image>();
            borderImage.color = cardData.GetRarityColor();

            // Icon placeholder
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(cardObj.transform);

            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0, -50);
            iconRect.sizeDelta = new Vector2(60, 60);

            var iconImage = iconObj.AddComponent<Image>();
            iconImage.color = cardData.GetRarityColor();

            // Card title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(cardObj.transform);

            var titleRectTransform = titleObj.AddComponent<RectTransform>();
            titleRectTransform.anchorMin = new Vector2(0, 0.3f);
            titleRectTransform.anchorMax = new Vector2(1, 0.5f);
            titleRectTransform.offsetMin = new Vector2(5, 0);
            titleRectTransform.offsetMax = new Vector2(-5, 0);

            var titleText = titleObj.AddComponent<Text>();
            titleText.text = cardData.cardName;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 14;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleCenter;

            // Count badge (if more than 1)
            if (count > 1)
            {
                GameObject badgeObj = new GameObject("CountBadge");
                badgeObj.transform.SetParent(cardObj.transform);

                var badgeRect = badgeObj.AddComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(1, 1);
                badgeRect.anchorMax = new Vector2(1, 1);
                badgeRect.anchoredPosition = new Vector2(-15, -15);
                badgeRect.sizeDelta = new Vector2(30, 30);

                var badgeImage = badgeObj.AddComponent<Image>();
                badgeImage.color = new Color(0.8f, 0.2f, 0.2f);

                GameObject badgeTextObj = new GameObject("BadgeText");
                badgeTextObj.transform.SetParent(badgeObj.transform);

                var badgeTextRect = badgeTextObj.AddComponent<RectTransform>();
                badgeTextRect.anchorMin = Vector2.zero;
                badgeTextRect.anchorMax = Vector2.one;
                badgeTextRect.offsetMin = Vector2.zero;
                badgeTextRect.offsetMax = Vector2.zero;

                var badgeText = badgeTextObj.AddComponent<Text>();
                badgeText.text = $"x{count}";
                badgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                badgeText.fontSize = 14;
                badgeText.color = Color.white;
                badgeText.alignment = TextAnchor.MiddleCenter;
            }

            cardObjects.Add(cardObj);
        }
    }
}
```

**Step 2: Verify script compiles**

Check Unity console for compilation errors.

---

### Task 5: Integrate UpgradeManager with GameManager

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`

**Step 1: Add UpgradeManager reference and modify OnWaveComplete**

In `GameManager.cs`, change the `OnWaveComplete` method to show upgrade selection BEFORE expanding the map:

Add field at top of class (around line 33):
```csharp
private UpgradeSelectionUI upgradeSelectionUI;
```

Modify `Start()` method to find the upgrade UI (add after line 76):
```csharp
upgradeSelectionUI = FindObjectOfType<UpgradeSelectionUI>();
if (upgradeSelectionUI != null)
{
    upgradeSelectionUI.OnCardSelected += OnUpgradeCardSelected;
}
```

Modify `OnDestroy()` to unsubscribe (add after line 87):
```csharp
if (upgradeSelectionUI != null)
{
    upgradeSelectionUI.OnCardSelected -= OnUpgradeCardSelected;
}
```

Replace `OnWaveComplete()` method (lines 167-171) with:
```csharp
private void OnWaveComplete()
{
    Debug.Log("Wave complete! Showing upgrade selection...");
    ShowUpgradeSelection();
}

private void ShowUpgradeSelection()
{
    if (UpgradeManager.Instance != null && upgradeSelectionUI != null)
    {
        var cards = UpgradeManager.Instance.GetRandomCards(3);
        if (cards.Count > 0)
        {
            upgradeSelectionUI.Show(cards);
        }
        else
        {
            // No cards configured, skip to expansion
            OnUpgradeCardSelected();
        }
    }
    else
    {
        // No upgrade system, skip to expansion
        ExpandMap();
    }
}

private void OnUpgradeCardSelected()
{
    Debug.Log("Upgrade selected! Expanding map...");
    ExpandMap();
}
```

**Step 2: Verify script compiles**

Check Unity console for compilation errors.

---

### Task 6: Modify Tower to Use Upgrade Bonuses

**Files:**
- Modify: `Assets/Scripts/Entities/Tower.cs`

**Step 1: Modify Fire() method to apply damage bonus**

In `Tower.cs`, replace the `Fire()` method (lines 166-196) with:

```csharp
private void Fire()
{
    if (currentTarget == null) return;

    float damageBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.TowerDamageBonus : 0f;
    float actualDamage = data.damage * (1f + damageBonus);
    int projectileCount = 1 + (UpgradeManager.Instance != null ? UpgradeManager.Instance.ExtraProjectiles : 0);

    if (data.appliesSlow)
    {
        // Slow tower - instant effect (apply to all targets if multiple projectiles)
        ApplySlowToTargets(projectileCount);
    }
    else if (data.isAreaDamage)
    {
        // Area damage
        ApplyAreaDamage(actualDamage);
    }
    else
    {
        // Single target damage with potential extra projectiles
        ApplySingleTargetDamage(actualDamage, projectileCount);
    }

    // Visual feedback - simple line
    Debug.DrawLine(turretHead.position, currentTarget.transform.position, Color.yellow, 0.1f);
}

private void ApplySlowToTargets(int projectileCount)
{
    var enemies = FindObjectsOfType<Enemy>();
    List<Enemy> validTargets = new List<Enemy>();

    foreach (var enemy in enemies)
    {
        if (!enemy.IsDead)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance <= data.range)
            {
                validTargets.Add(enemy);
            }
        }
    }

    // Sort by distance
    validTargets.Sort((a, b) =>
        Vector3.Distance(transform.position, a.transform.position)
        .CompareTo(Vector3.Distance(transform.position, b.transform.position)));

    // Apply slow to closest N targets
    for (int i = 0; i < Mathf.Min(projectileCount, validTargets.Count); i++)
    {
        validTargets[i].ApplySlow(data.slowMultiplier, data.slowDuration);
    }
}

private void ApplyAreaDamage(float damage)
{
    var enemies = FindObjectsOfType<Enemy>();
    foreach (var enemy in enemies)
    {
        float distance = Vector3.Distance(currentTarget.transform.position, enemy.transform.position);
        if (distance <= data.areaRadius)
        {
            enemy.TakeDamage(damage);
        }
    }
}

private void ApplySingleTargetDamage(float damage, int projectileCount)
{
    if (projectileCount == 1)
    {
        currentTarget.TakeDamage(damage);
    }
    else
    {
        // Multiple projectiles - hit closest enemies
        var enemies = FindObjectsOfType<Enemy>();
        List<Enemy> validTargets = new List<Enemy>();

        foreach (var enemy in enemies)
        {
            if (!enemy.IsDead)
            {
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance <= data.range)
                {
                    validTargets.Add(enemy);
                }
            }
        }

        // Sort by distance
        validTargets.Sort((a, b) =>
            Vector3.Distance(transform.position, a.transform.position)
            .CompareTo(Vector3.Distance(transform.position, b.transform.position)));

        // Hit closest N targets
        for (int i = 0; i < Mathf.Min(projectileCount, validTargets.Count); i++)
        {
            validTargets[i].TakeDamage(damage);
            Debug.DrawLine(turretHead.position, validTargets[i].transform.position, Color.yellow, 0.1f);
        }
    }
}
```

**Step 2: Modify Update() to apply speed bonus to fire rate**

In `Tower.cs`, replace the fire rate calculation in `Update()` (line 109) with:

```csharp
float speedBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.TowerSpeedBonus : 0f;
fireCooldown = 1f / (data.fireRate * (1f + speedBonus));
```

**Step 3: Add using statement at top of file**

Add after line 2:
```csharp
using TowerDefense.Core;
```

**Step 4: Verify script compiles**

Check Unity console for compilation errors.

---

### Task 7: Modify Enemy to Use Upgrade Bonuses

**Files:**
- Modify: `Assets/Scripts/Entities/Enemy.cs`

**Step 1: Modify Initialize() to apply speed bonus**

In `Enemy.cs`, replace the speed calculation in `Initialize()` (line 32) with:

```csharp
float speedBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.EnemySpeedBonus : 0f;
currentSpeed = (baseSpeed + (waveNumber - 1) * 0.1f) * (1f + speedBonus);
```

**Step 2: Modify Die() to apply gold bonus**

In `Enemy.cs`, replace the `Die()` method (lines 133-138) with:

```csharp
private void Die()
{
    float goldBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.EnemyGoldBonus : 0f;
    int actualReward = Mathf.RoundToInt(currencyReward * (1f + goldBonus));
    GameManager.Instance?.AddCurrency(actualReward);
    OnDeath?.Invoke(this);
    Destroy(gameObject);
}
```

**Step 3: Verify script compiles**

Check Unity console for compilation errors.

---

### Task 8: Create ScriptableObject Assets for Cards

**Files:**
- Create: `Assets/ScriptableObjects/Upgrades/SwiftTowers.asset`
- Create: `Assets/ScriptableObjects/Upgrades/HeavyRounds.asset`
- Create: `Assets/ScriptableObjects/Upgrades/RiskyInvestment.asset`
- Create: `Assets/ScriptableObjects/Upgrades/Barrage.asset`

**Step 1: Create the folder structure**

In Unity Editor:
1. Right-click Assets folder → Create → Folder → Name it "ScriptableObjects"
2. Right-click ScriptableObjects → Create → Folder → Name it "Upgrades"

**Step 2: Create Swift Towers card**

1. Right-click Upgrades folder → Create → Tower Defense → Upgrade Card
2. Name it "SwiftTowers"
3. Set properties:
   - Card Name: "Swift Towers"
   - Description: "Tower attack speed +5%"
   - Rarity: Common
   - Effect Type: TowerSpeed
   - Effect Value: 0.05
   - Secondary Value: 0
   - Icon Shape: Circle

**Step 3: Create Heavy Rounds card**

1. Right-click Upgrades folder → Create → Tower Defense → Upgrade Card
2. Name it "HeavyRounds"
3. Set properties:
   - Card Name: "Heavy Rounds"
   - Description: "Tower attack damage +5%"
   - Rarity: Common
   - Effect Type: TowerDamage
   - Effect Value: 0.05
   - Secondary Value: 0
   - Icon Shape: Diamond

**Step 4: Create Risky Investment card**

1. Right-click Upgrades folder → Create → Tower Defense → Upgrade Card
2. Name it "RiskyInvestment"
3. Set properties:
   - Card Name: "Risky Investment"
   - Description: "Enemies move 10% faster but give 20% more gold"
   - Rarity: Rare
   - Effect Type: EnemySpeedAndGold
   - Effect Value: 0.1
   - Secondary Value: 0.2
   - Icon Shape: Star

**Step 5: Create Barrage card**

1. Right-click Upgrades folder → Create → Tower Defense → Upgrade Card
2. Name it "Barrage"
3. Set properties:
   - Card Name: "Barrage"
   - Description: "Towers fire +1 additional projectile"
   - Rarity: Epic
   - Effect Type: ExtraProjectiles
   - Effect Value: 1
   - Secondary Value: 0
   - Icon Shape: Hexagon

---

### Task 9: Set Up Scene with Upgrade System

**Step 1: Create UpgradeManager GameObject**

1. In Unity Hierarchy, create empty GameObject named "UpgradeManager"
2. Add UpgradeManager component
3. In the Inspector, drag all 4 upgrade card assets into the "All Upgrade Cards" list

**Step 2: Create UI GameObjects**

1. Find the existing HUD_Canvas (created by HUDController)
2. Create empty child GameObject named "UpgradeSelectionUI"
3. Add UpgradeSelectionUI component
4. Create another empty child GameObject named "PickedCardsUI"
5. Add PickedCardsUI component

Alternatively, modify HUDController to create these automatically.

---

### Task 10: Modify HUDController to Create Upgrade UIs

**Files:**
- Modify: `Assets/Scripts/UI/HUDController.cs`

**Step 1: Add upgrade UI creation in CreateUI() method**

Add at the end of `CreateUI()` method (before the closing brace, around line 100):

```csharp
// Create upgrade selection UI
var upgradeSelectionObj = new GameObject("UpgradeSelectionUI");
upgradeSelectionObj.transform.SetParent(canvasObj.transform);
upgradeSelectionObj.AddComponent<UpgradeSelectionUI>();

// Create picked cards UI
var pickedCardsObj = new GameObject("PickedCardsUI");
pickedCardsObj.transform.SetParent(canvasObj.transform);
pickedCardsObj.AddComponent<PickedCardsUI>();
```

**Step 2: Verify script compiles**

Check Unity console for compilation errors.

---

### Task 11: Test the Complete System

**Step 1: Enter Play Mode and complete a wave**

1. Press Play in Unity Editor
2. Click "Start Wave"
3. Wait for all enemies to be defeated or reach castle
4. Verify the upgrade selection overlay appears
5. Verify game is paused (Time.timeScale = 0)

**Step 2: Test card selection**

1. Click on one of the 3 cards
2. Verify overlay closes
3. Verify game resumes
4. Verify map expansion happens

**Step 3: Test picked cards viewer**

1. After picking at least 1 card, verify button appears in bottom-left
2. Click the button
3. Verify overlay shows with picked card(s)
4. Click outside cards to close

**Step 4: Test upgrade effects**

1. Pick "Swift Towers" multiple times
2. Build a tower and observe faster fire rate
3. Pick "Heavy Rounds" and verify increased damage
4. Pick "Barrage" and verify tower hits multiple enemies

---

### Task 12: Handle Game Over Reset

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`

**Step 1: Reset upgrades on game over**

Add to the game over handling. In the `LoseLife()` method, after setting `gameOver = true` (around line 155), add:

```csharp
// Reset upgrades for new run
UpgradeManager.Instance?.ResetForNewRun();
```

**Step 2: Verify script compiles**

Check Unity console for compilation errors.
