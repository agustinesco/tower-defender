using TMPro;
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
        [SerializeField] private TextMeshProUGUI resourceText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button labButton;
        [SerializeField] private Button resetButton;

        [Header("Lab Panel")]
        [SerializeField] private GameObject labPanel;
        [SerializeField] private TextMeshProUGUI labResourceLabel;
        [SerializeField] private Transform labGridContent;
        [SerializeField] private Button labBackButton;
        [SerializeField] private Image[] labTabImages;
        [SerializeField] private TextMeshProUGUI[] labTabTexts;
        [SerializeField] private Button[] labTabButtons;

        [Header("Quest Panel")]
        [SerializeField] private GameObject questPanel;
        [SerializeField] private TextMeshProUGUI questResourceLabel;
        [SerializeField] private Transform questGridContent;
        [SerializeField] private Button questBackButton;
        [SerializeField] private Button questAreaButton;

        [Header("Tutorial")]
        [SerializeField] private Sprite tutorialArrowSprite;

        private int activeLabTab;
        private List<GameObject> labUpgradeCards = new List<GameObject>();
        private List<GameObject> questCards = new List<GameObject>();

        private static readonly Color[] LabTabActiveColors = {
            new Color(0.25f, 0.3f, 0.55f),
            new Color(0.55f, 0.25f, 0.25f),
            new Color(0.25f, 0.45f, 0.3f)
        };
        private static readonly Color LabTabInactiveColor = new Color(0.18f, 0.18f, 0.22f);

        // Reset confirmation
        private GameObject resetConfirmOverlay;

        // Tutorial state
        private bool labTutorialActive;
        private int labTutorialStep; // 0 = map, 1 = lab
        private bool questTutorialActive;
        private int questTutorialStep; // 0 = quest board, 1 = accept, 2 = back, 3 = play
        private GameObject tutPanelObj;
        private RectTransform tutPanelRect;
        private TextMeshProUGUI tutMessageText;
        private GameObject tutArrowObj;
        private RectTransform tutArrowRect;
        private RectTransform tutTargetRect;
        private Vector2 tutArrowBounceDir;
        private RectTransform safeAreaRect;

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
            if (QuestManager.Instance == null)
            {
                var questObj = new GameObject("QuestManager");
                questObj.AddComponent<QuestManager>();
            }
        }

        private void Start()
        {
            Time.timeScale = 1f;

            if (startButton != null) startButton.onClick.AddListener(OnContinuousClicked);
            if (labButton != null) labButton.onClick.AddListener(OnLabClicked);
            if (labBackButton != null) labBackButton.onClick.AddListener(OnLabBackClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
            if (questBackButton != null) questBackButton.onClick.AddListener(OnQuestBackClicked);
            if (questAreaButton != null) questAreaButton.onClick.AddListener(OnQuestAreaClicked);

            if (labTabButtons != null)
            {
                for (int i = 0; i < labTabButtons.Length; i++)
                {
                    int tabIndex = i;
                    labTabButtons[i].onClick.AddListener(() => OnLabTabClicked(tabIndex));
                }
            }

            ShowMap();
            CheckQuestTutorial();
            if (!questTutorialActive)
                CheckLabTutorial();
        }

        private void Update()
        {
            if ((labTutorialActive || questTutorialActive) && tutTargetRect != null)
                UpdateTutArrowPosition();
        }

        // --- Tutorial ---

        private void CheckLabTutorial()
        {
            if (JsonSaveSystem.Data.labTutorialComplete) return;
            if (!HasAffordableUpgrade()) return;

            // Cache SafeArea rect from mapPanel's parent
            if (mapPanel != null)
                safeAreaRect = mapPanel.transform.parent as RectTransform;
            if (safeAreaRect == null) return;

            labTutorialActive = true;
            labTutorialStep = 0;
            CreateTutorialUI();
            ShowLabTutorialStep();
        }

        // --- Quest Tutorial ---

        private void CheckQuestTutorial()
        {
            if (JsonSaveSystem.Data.questTutorialComplete) return;

            if (mapPanel != null)
                safeAreaRect = mapPanel.transform.parent as RectTransform;
            if (safeAreaRect == null) return;

            questTutorialActive = true;
            questTutorialStep = 0;
            CreateTutorialUI();
            ShowQuestTutorialStep();
        }

        private void ShowQuestTutorialStep()
        {
            switch (questTutorialStep)
            {
                case 0:
                    tutMessageText.text = "Pick a quest to begin your expedition!";
                    if (questAreaButton != null)
                    {
                        PositionPanelNearTarget((RectTransform)questAreaButton.transform, new Vector2(0f, 120f));
                        PointTutArrowAt((RectTransform)questAreaButton.transform, new Vector2(0f, 1f));
                    }
                    break;
                case 1:
                    tutMessageText.text = "Accept this quest";
                    var acceptRect = GetFirstQuestAcceptButtonRect();
                    if (acceptRect != null)
                    {
                        PositionPanelNearTarget(acceptRect, new Vector2(0f, 120f));
                        PointTutArrowAt(acceptRect, new Vector2(0f, 1f));
                    }
                    break;
                case 2:
                    tutMessageText.text = "Head back to the map";
                    if (questBackButton != null)
                    {
                        PositionPanelNearTarget((RectTransform)questBackButton.transform, new Vector2(0f, 120f));
                        PointTutArrowAt((RectTransform)questBackButton.transform, new Vector2(0f, 1f));
                    }
                    break;
                case 3:
                    tutMessageText.text = "Start your expedition!";
                    if (startButton != null)
                    {
                        PositionPanelNearTarget((RectTransform)startButton.transform, new Vector2(0f, 120f));
                        PointTutArrowAt((RectTransform)startButton.transform, new Vector2(0f, 1f));
                    }
                    break;
            }
        }

        private void PositionPanelNearTarget(RectTransform target, Vector2 offset)
        {
            if (target == null || safeAreaRect == null) return;
            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, target.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                safeAreaRect, screenPos, null, out Vector2 localPoint))
            {
                tutPanelRect.anchoredPosition = localPoint + offset;
            }
        }

        private RectTransform GetFirstQuestAcceptButtonRect()
        {
            if (questCards.Count == 0) return null;
            var firstCard = questCards[0];
            if (firstCard == null) return null;
            var actionBtn = firstCard.transform.Find("ActionBtn");
            if (actionBtn != null)
                return actionBtn.GetComponent<RectTransform>();
            return null;
        }

        private void CompleteQuestTutorial()
        {
            JsonSaveSystem.Data.questTutorialComplete = true;
            JsonSaveSystem.Save();
            if (tutPanelObj != null) Destroy(tutPanelObj);
            if (tutArrowObj != null) Destroy(tutArrowObj);
            questTutorialActive = false;
        }

        private bool HasAffordableUpgrade()
        {
            if (LabManager.Instance == null) return false;
            var upgrades = LabManager.Instance.Upgrades;
            for (int i = 0; i < upgrades.Count; i++)
            {
                // Skip quest-granted unlocks (hidden from the lab UI)
                if (upgrades[i].baseCost == 0 && upgrades[i].upgradeType == LabUpgradeType.TowerUnlock) continue;
                if (LabManager.Instance.CanPurchase(upgrades[i]))
                    return true;
            }
            return false;
        }

        private void CreateTutorialUI()
        {
            // Panel
            tutPanelObj = new GameObject("TutorialPanel");
            tutPanelObj.transform.SetParent(safeAreaRect, false);
            tutPanelRect = tutPanelObj.AddComponent<RectTransform>();
            tutPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            tutPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            tutPanelRect.sizeDelta = new Vector2(600f, 100f);
            var panelBg = tutPanelObj.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
            panelBg.raycastTarget = false;

            var textObj = new GameObject("Message");
            textObj.transform.SetParent(tutPanelObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 8f);
            textRect.offsetMax = new Vector2(-16f, -8f);
            tutMessageText = textObj.AddComponent<TextMeshProUGUI>();
            tutMessageText.fontSize = 22;
            tutMessageText.fontStyle = FontStyles.Bold;
            tutMessageText.color = Color.white;
            tutMessageText.alignment = TextAlignmentOptions.Center;
            tutMessageText.raycastTarget = false;

            // Arrow
            tutArrowObj = new GameObject("TutorialArrow");
            tutArrowObj.transform.SetParent(safeAreaRect, false);
            tutArrowRect = tutArrowObj.AddComponent<RectTransform>();
            tutArrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            tutArrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            tutArrowRect.sizeDelta = new Vector2(60f, 60f);
            var arrowImg = tutArrowObj.AddComponent<Image>();
            arrowImg.sprite = tutorialArrowSprite;
            arrowImg.color = new Color(1f, 0.3f, 0.3f, 1f);
            arrowImg.raycastTarget = false;
        }

        private void ShowLabTutorialStep()
        {
            if (labTutorialStep == 0)
            {
                tutMessageText.text = "You earned resources! Visit the Lab to power up";
                // Position panel above center
                tutPanelRect.anchoredPosition = new Vector2(0f, 100f);
                // Point arrow at Lab button
                if (labButton != null)
                    PointTutArrowAt((RectTransform)labButton.transform, new Vector2(0f, 1f));
            }
            else if (labTutorialStep == 1)
            {
                tutMessageText.text = "Purchase an upgrade to improve your towers!";
                // Position panel near top of lab area
                tutPanelRect.anchoredPosition = new Vector2(0f, 200f);
                // Point arrow at first affordable card
                var cardRect = GetFirstAffordableCardRect();
                if (cardRect != null)
                    PointTutArrowAt(cardRect, new Vector2(0f, 1f));
            }
        }

        private void PointTutArrowAt(RectTransform target, Vector2 bounceDir)
        {
            tutTargetRect = target;
            tutArrowBounceDir = bounceDir;

            // Rotate arrow to face toward the target (opposite of bounce direction)
            float angle = Mathf.Atan2(-bounceDir.y, -bounceDir.x) * Mathf.Rad2Deg;
            tutArrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            UpdateTutArrowPosition();
        }

        private void UpdateTutArrowPosition()
        {
            if (tutTargetRect == null || safeAreaRect == null) return;

            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, tutTargetRect.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                safeAreaRect, screenPos, null, out Vector2 localPoint))
            {
                float bounce = Mathf.Sin(Time.unscaledTime * 3f) * 15f;
                Vector2 offset = tutArrowBounceDir * (40f + bounce);
                tutArrowRect.anchoredPosition = localPoint + offset;
            }
        }

        private void CompleteLabTutorial()
        {
            JsonSaveSystem.Data.labTutorialComplete = true;
            JsonSaveSystem.Save();
            if (tutPanelObj != null) Destroy(tutPanelObj);
            if (tutArrowObj != null) Destroy(tutArrowObj);
            labTutorialActive = false;
        }

        private RectTransform GetFirstAffordableCardRect()
        {
            if (LabManager.Instance == null) return null;
            var upgrades = LabManager.Instance.Upgrades;
            int cardIndex = 0;
            for (int i = 0; i < upgrades.Count; i++)
            {
                var upgrade = upgrades[i];
                if (!UpgradeMatchesTab(upgrade, activeLabTab)) continue;
                if (LabManager.Instance.CanPurchase(upgrade) && cardIndex < labUpgradeCards.Count)
                    return labUpgradeCards[cardIndex].GetComponent<RectTransform>();
                cardIndex++;
            }
            return null;
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
            if (questPanel != null) questPanel.SetActive(false);
        }

        private void OnContinuousClicked()
        {
            if (labTutorialActive) return;
            if (questTutorialActive && questTutorialStep != 3) return;
            if (questTutorialActive && questTutorialStep == 3)
                CompleteQuestTutorial();
            if (QuestManager.Instance != null && !QuestManager.Instance.HasActiveQuest)
            {
                Debug.Log("No quest selected! Open the Quest panel to accept a quest first.");
                return;
            }
            GameModeSelection.SelectedMode = GameMode.Continuous;
            SceneManager.LoadScene(1);
        }

        private void OnLabClicked()
        {
            if (questTutorialActive) return;
            if (mapPanel != null) mapPanel.SetActive(false);
            ShowLab();

            if (labTutorialActive)
            {
                labTutorialStep = 1;
                ShowLabTutorialStep();
            }
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
                // Hide quest-granted unlocks (baseCost 0) from the lab
                if (upgrade.baseCost == 0 && upgrade.upgradeType == LabUpgradeType.TowerUnlock) continue;
                labUpgradeCards.Add(CreateLabUpgradeCard(upgrade));
            }

            if (labTutorialActive && labTutorialStep == 1)
            {
                var cardRect = GetFirstAffordableCardRect();
                if (cardRect != null)
                    PointTutArrowAt(cardRect, new Vector2(0f, 1f));
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
            var nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = upgrade.upgradeName;
            nameText.fontSize = 20;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.raycastTarget = false;

            // Description
            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(card.transform);
            var descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.38f);
            descRect.anchorMax = new Vector2(1, 0.65f);
            descRect.offsetMin = new Vector2(16, 0);
            descRect.offsetMax = new Vector2(-8, 0);
            var descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = upgrade.description;
            descText.fontSize = 14;
            descText.color = new Color(0.7f, 0.7f, 0.7f);
            descText.alignment = TextAlignmentOptions.TopLeft;
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
            var levelText = levelObj.AddComponent<TextMeshProUGUI>();
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
            levelText.fontSize = 16;
            levelText.alignment = TextAlignmentOptions.Left;
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
                var buyText = buyTextObj.AddComponent<TextMeshProUGUI>();
                buyText.text = $"Buy {cost} {GetResourceShortName(upgrade.costResource)}";
                buyText.fontSize = 14;
                buyText.color = canBuy ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                buyText.alignment = TextAlignmentOptions.Center;
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
                var maxText = maxObj.AddComponent<TextMeshProUGUI>();
                maxText.text = "MAX";
                maxText.fontSize = 20;
                maxText.color = new Color(1f, 0.85f, 0.2f);
                maxText.alignment = TextAlignmentOptions.Center;
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

                if (labTutorialActive)
                    CompleteLabTutorial();
            }
        }

        private void OnLabBackClicked()
        {
            if (labTutorialActive) return;
            if (labPanel != null) labPanel.SetActive(false);
            ShowMap();
        }

        // --- Reset progress ---

        private void OnResetClicked()
        {
            if (labTutorialActive || questTutorialActive) return;
            if (resetConfirmOverlay != null) return;
            ShowResetConfirmation();
        }

        private void ShowResetConfirmation()
        {
            var parent = mapPanel != null ? mapPanel.transform.parent : transform;

            resetConfirmOverlay = new GameObject("ResetConfirm");
            resetConfirmOverlay.transform.SetParent(parent, false);
            var overlayRect = resetConfirmOverlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            // Dim background
            var dimImg = resetConfirmOverlay.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.7f);

            // Dialog box
            var dialog = new GameObject("Dialog");
            dialog.transform.SetParent(resetConfirmOverlay.transform, false);
            var dialogRect = dialog.AddComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(500f, 200f);
            var dialogBg = dialog.AddComponent<Image>();
            dialogBg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

            // Warning text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(dialog.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.05f, 0.5f);
            textRect.anchorMax = new Vector2(0.95f, 0.9f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "Reset all progress?\nThis cannot be undone.";
            text.fontSize = 22;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            // Confirm button
            var confirmObj = new GameObject("Confirm");
            confirmObj.transform.SetParent(dialog.transform, false);
            var confirmRect = confirmObj.AddComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.55f, 0.08f);
            confirmRect.anchorMax = new Vector2(0.95f, 0.4f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
            var confirmImg = confirmObj.AddComponent<Image>();
            confirmImg.color = new Color(0.7f, 0.2f, 0.2f);
            var confirmBtn = confirmObj.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmImg;
            confirmBtn.onClick.AddListener(OnResetConfirmed);

            var confirmTextObj = new GameObject("Text");
            confirmTextObj.transform.SetParent(confirmObj.transform, false);
            var ctRect = confirmTextObj.AddComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.offsetMin = Vector2.zero;
            ctRect.offsetMax = Vector2.zero;
            var ctText = confirmTextObj.AddComponent<TextMeshProUGUI>();
            ctText.text = "Reset";
            ctText.fontSize = 20;
            ctText.fontStyle = FontStyles.Bold;
            ctText.color = Color.white;
            ctText.alignment = TextAlignmentOptions.Center;

            // Cancel button
            var cancelObj = new GameObject("Cancel");
            cancelObj.transform.SetParent(dialog.transform, false);
            var cancelRect = cancelObj.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.05f, 0.08f);
            cancelRect.anchorMax = new Vector2(0.45f, 0.4f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            var cancelImg = cancelObj.AddComponent<Image>();
            cancelImg.color = new Color(0.3f, 0.3f, 0.35f);
            var cancelBtn = cancelObj.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(OnResetCancelled);

            var cancelTextObj = new GameObject("Text");
            cancelTextObj.transform.SetParent(cancelObj.transform, false);
            var clRect = cancelTextObj.AddComponent<RectTransform>();
            clRect.anchorMin = Vector2.zero;
            clRect.anchorMax = Vector2.one;
            clRect.offsetMin = Vector2.zero;
            clRect.offsetMax = Vector2.zero;
            var clText = cancelTextObj.AddComponent<TextMeshProUGUI>();
            clText.text = "Cancel";
            clText.fontSize = 20;
            clText.color = Color.white;
            clText.alignment = TextAlignmentOptions.Center;
        }

        private void OnResetConfirmed()
        {
            JsonSaveSystem.DeleteAll();
            if (QuestManager.Instance != null)
                QuestManager.Instance.ReloadFromSave();
            SceneManager.LoadScene(0);
        }

        private void OnResetCancelled()
        {
            if (resetConfirmOverlay != null)
            {
                Destroy(resetConfirmOverlay);
                resetConfirmOverlay = null;
            }
        }

        // --- Quest Panel ---

        private void OnQuestAreaClicked()
        {
            if (mapPanel != null) mapPanel.SetActive(false);
            ShowQuestPanel();

            if (questTutorialActive && questTutorialStep == 0)
            {
                questTutorialStep = 1;
                ShowQuestTutorialStep();
            }
        }

        private void ShowQuestPanel()
        {
            UpdateQuestResources();
            RefreshQuestCards();
            if (questPanel != null) questPanel.SetActive(true);
        }

        private void OnQuestBackClicked()
        {
            if (questTutorialActive && questTutorialStep < 2) return;
            if (questPanel != null) questPanel.SetActive(false);
            ShowMap();

            if (questTutorialActive && questTutorialStep == 2)
            {
                questTutorialStep = 3;
                ShowQuestTutorialStep();
            }
        }

        private void UpdateQuestResources()
        {
            if (questResourceLabel == null || PersistenceManager.Instance == null) return;

            var pm = PersistenceManager.Instance;
            questResourceLabel.text = string.Format(
                "Iron: {0}    Gems: {1}    Florpus: {2}    Adamantite: {3}",
                pm.GetBanked(ResourceType.IronOre),
                pm.GetBanked(ResourceType.Gems),
                pm.GetBanked(ResourceType.Florpus),
                pm.GetBanked(ResourceType.Adamantite)
            );
        }

        private void RefreshQuestCards()
        {
            foreach (var card in questCards)
            {
                if (card != null) Destroy(card);
            }
            questCards.Clear();

            if (QuestManager.Instance == null || questGridContent == null) return;

            var quests = QuestManager.Instance.AllQuests;
            for (int i = 0; i < quests.Count; i++)
            {
                if (!QuestManager.Instance.IsQuestUnlocked(quests[i].questId))
                    continue;
                questCards.Add(CreateQuestCard(quests[i]));
            }
        }

        private GameObject CreateQuestCard(QuestDefinition quest)
        {
            var qm = QuestManager.Instance;
            bool isActive = qm.ActiveQuestId == quest.questId;
            bool isCompleted = qm.IsQuestCompleted(quest.questId);

            var card = new GameObject($"QuestCard_{quest.questId}");
            card.transform.SetParent(questGridContent);
            card.AddComponent<RectTransform>();

            var cardBg = card.AddComponent<Image>();
            cardBg.color = isActive ? new Color(0.15f, 0.2f, 0.15f, 0.9f) : new Color(0.14f, 0.14f, 0.2f, 0.9f);

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
            accentImg.color = GetResourceColor(quest.rewardResource);
            accentImg.raycastTarget = false;

            // Name
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(card.transform);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.72f);
            nameRect.anchorMax = new Vector2(1, 0.98f);
            nameRect.offsetMin = new Vector2(16, 0);
            nameRect.offsetMax = new Vector2(-8, 0);
            var nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = quest.questName;
            nameText.fontSize = 20;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.raycastTarget = false;

            // Objectives text
            var objObj = new GameObject("Objectives");
            objObj.transform.SetParent(card.transform);
            var objRect = objObj.AddComponent<RectTransform>();
            objRect.anchorMin = new Vector2(0, 0.38f);
            objRect.anchorMax = new Vector2(1, 0.72f);
            objRect.offsetMin = new Vector2(16, 0);
            objRect.offsetMax = new Vector2(-8, 0);
            var objText = objObj.AddComponent<TextMeshProUGUI>();
            objText.text = BuildObjectiveSummary(quest);
            objText.fontSize = 14;
            objText.color = new Color(0.7f, 0.7f, 0.7f);
            objText.alignment = TextAlignmentOptions.TopLeft;
            objText.raycastTarget = false;

            // Reward text
            var rewardObj = new GameObject("Reward");
            rewardObj.transform.SetParent(card.transform);
            var rewardRect = rewardObj.AddComponent<RectTransform>();
            rewardRect.anchorMin = new Vector2(0, 0.02f);
            rewardRect.anchorMax = new Vector2(0.45f, 0.35f);
            rewardRect.offsetMin = new Vector2(16, 0);
            rewardRect.offsetMax = Vector2.zero;
            var rewardText = rewardObj.AddComponent<TextMeshProUGUI>();
            string rewardStr = quest.rewardAmount > 0
                ? $"Reward: {quest.rewardAmount} {GetResourceShortName(quest.rewardResource)}"
                : "Reward:";
            if (!string.IsNullOrEmpty(quest.unlockLabUpgrade))
                rewardStr += (quest.rewardAmount > 0 ? " + " : " ") + $"Unlock {quest.unlockLabUpgrade}";
            rewardText.text = rewardStr;
            rewardText.fontSize = 14;
            rewardText.color = new Color(1f, 0.85f, 0.2f);
            rewardText.alignment = TextAlignmentOptions.Left;
            rewardText.raycastTarget = false;

            // Action button
            var btnObj = new GameObject("ActionBtn");
            btnObj.transform.SetParent(card.transform);
            var btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.05f);
            btnRect.anchorMax = new Vector2(0.95f, 0.35f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnImage = btnObj.AddComponent<Image>();
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImage;

            var btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform);
            var btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
            var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.fontSize = 14;
            btnText.alignment = TextAlignmentOptions.Center;

            if (isActive)
            {
                btnImage.color = new Color(0.5f, 0.5f, 0.2f);
                btnText.text = "ACTIVE";
                btnText.color = Color.white;
                btn.interactable = false;
            }
            else if (isCompleted)
            {
                btnImage.color = new Color(0.2f, 0.5f, 0.2f);
                btnText.text = "Replay";
                btnText.color = Color.white;
                var capturedId = quest.questId;
                btn.onClick.AddListener(() => OnAcceptQuest(capturedId));
            }
            else
            {
                btnImage.color = new Color(0.2f, 0.4f, 0.6f);
                btnText.text = "Accept";
                btnText.color = Color.white;
                var capturedId = quest.questId;
                btn.onClick.AddListener(() => OnAcceptQuest(capturedId));
            }

            return card;
        }

        private string BuildObjectiveSummary(QuestDefinition quest)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var obj = quest.objectives[i];
                if (i > 0) sb.Append("\n");
                switch (obj.objectiveType)
                {
                    case QuestObjectiveType.GatherResource:
                        sb.Append($"Gather {obj.requiredAmount} {GetResourceShortName(obj.resourceType)}");
                        break;
                    case QuestObjectiveType.KillEnemies:
                        sb.Append($"Kill {obj.requiredAmount} enemies");
                        break;
                    case QuestObjectiveType.ExpandTiles:
                        sb.Append($"Place {obj.requiredAmount} tiles");
                        break;
                }
            }
            return sb.ToString();
        }

        private void OnAcceptQuest(string questId)
        {
            if (QuestManager.Instance == null) return;
            QuestManager.Instance.AcceptQuest(questId);
            RefreshQuestCards();

            if (questTutorialActive && questTutorialStep == 1)
            {
                questTutorialStep = 2;
                ShowQuestTutorialStep();
            }
        }

        // --- Static helper ---

        public static void LoadMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }
    }
}
