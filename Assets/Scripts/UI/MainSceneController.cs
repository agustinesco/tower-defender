using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using DG.Tweening;
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
        [SerializeField] private GameObject labUpgradePrefab;

        [Header("Quest Panel")]
        [SerializeField] private GameObject questPanel;
        [SerializeField] private TextMeshProUGUI questResourceLabel;
        [SerializeField] private Transform questGridContent;
        [SerializeField] private Button questBackButton;
        [SerializeField] private Button questAreaButton;
        [SerializeField] private Button questStartButton;
        [SerializeField] private GameObject questCardPrefab;
        [SerializeField] private GameObject questDetailPrefab;

        [Header("Notifications")]
        [SerializeField] private GameObject questNotifyObj;

        [Header("Tutorial")]
        [SerializeField] private Sprite tutorialArrowSprite;

        private int activeLabTab;
        private List<GameObject> labUpgradeCards = new List<GameObject>();
        private List<GameObject> questCards = new List<GameObject>();

        private static readonly Color[] LabTabActiveColors = {
            new Color(0.25f, 0.3f, 0.55f),
            new Color(0.55f, 0.25f, 0.25f),
            new Color(0.25f, 0.45f, 0.3f),
            new Color(0.45f, 0.35f, 0.2f)
        };
        private static readonly Color LabTabInactiveColor = new Color(0.18f, 0.18f, 0.22f);

        // Reset confirmation
        private GameObject resetConfirmOverlay;

        // Quest detail popup
        private GameObject questDetailOverlay;
        private RectTransform questDetailAcceptBtnRect;

        // Quest button notification
        private RectTransform questNotifyRect;
        private float questNotifyStartY;

        // Tutorial state
        private bool labTutorialActive;
        private int labTutorialStep; // 0 = map, 1 = lab
        private bool questTutorialActive;
        private int questTutorialStep; // 0 = quest board, 1 = accept, 2 = start run
        private GameObject tutPanelObj;
        private RectTransform tutPanelRect;
        private TextMeshProUGUI tutMessageText;
        private GameObject tutArrowObj;
        private RectTransform tutArrowRect;
        private RectTransform tutTargetRect;
        private Vector2 tutArrowBounceDir;
        private RectTransform safeAreaRect;
        private bool tutPanelTracksTarget;
        private Vector2 tutPanelOffset;

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

            // Main menu start button now opens quest panel
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnQuestAreaClicked);
                var label = startButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = "Quests";
            }
            if (labButton != null) labButton.onClick.AddListener(OnLabClicked);
            if (labBackButton != null) labBackButton.onClick.AddListener(OnLabBackClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
            if (questBackButton != null) questBackButton.onClick.AddListener(OnQuestBackClicked);
            if (questAreaButton != null) questAreaButton.onClick.AddListener(OnQuestAreaClicked);
            if (questStartButton != null) questStartButton.onClick.AddListener(OnContinuousClicked);

            if (labTabButtons != null)
            {
                for (int i = 0; i < labTabButtons.Length; i++)
                {
                    int tabIndex = i;
                    labTabButtons[i].onClick.AddListener(() => OnLabTabClicked(tabIndex));
                }
            }

            ShowMap();
            UpdateQuestStartButton();
            CheckQuestTutorial();
            if (!questTutorialActive)
                CheckLabTutorial();
        }

        private void Update()
        {
            if ((labTutorialActive || questTutorialActive) && tutTargetRect != null)
            {
                UpdateTutArrowPosition();
                if (tutPanelTracksTarget)
                    UpdateTutPanelPosition();
            }
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
                    // If detail popup is open, point at its Accept button
                    if (questDetailAcceptBtnRect != null)
                    {
                        tutMessageText.text = "Accept this quest";
                        PositionPanelNearTarget(questDetailAcceptBtnRect, new Vector2(0f, 120f));
                        PointTutArrowAt(questDetailAcceptBtnRect, new Vector2(0f, 1f));
                    }
                    else
                    {
                        tutMessageText.text = "Tap a quest to view details";
                        if (questCards.Count > 0 && questCards[0] != null)
                        {
                            var cardRect = questCards[0].GetComponent<RectTransform>();
                            PositionPanelNearTarget(cardRect, new Vector2(0f, 120f));
                            PointTutArrowAt(cardRect, new Vector2(0f, 1f));
                        }
                    }
                    break;
                case 2:
                    tutMessageText.text = "Start your expedition!";
                    if (questStartButton != null)
                    {
                        PositionPanelNearTarget((RectTransform)questStartButton.transform, new Vector2(0f, 120f));
                        PointTutArrowAt((RectTransform)questStartButton.transform, new Vector2(0f, 1f));
                    }
                    break;
            }
        }

        private void PositionPanelNearTarget(RectTransform target, Vector2 offset)
        {
            tutPanelTracksTarget = true;
            tutPanelOffset = offset;
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
            return questDetailAcceptBtnRect;
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
            tutPanelTracksTarget = false;
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

        private void UpdateTutPanelPosition()
        {
            if (tutTargetRect == null || safeAreaRect == null || tutPanelRect == null) return;

            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, tutTargetRect.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                safeAreaRect, screenPos, null, out Vector2 localPoint))
            {
                tutPanelRect.anchoredPosition = localPoint + tutPanelOffset;
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
            CheckQuestButtonHighlight();
        }

        private void CheckQuestButtonHighlight()
        {
            if (questNotifyObj == null) return;

            var qm = QuestManager.Instance;
            if (qm == null)
            {
                questNotifyObj.SetActive(false);
                return;
            }

            // Show when active quest is completed or no quest is active,
            // and there are unlocked uncompleted quests to pick
            bool needsNewQuest = !qm.HasActiveQuest || qm.IsQuestCompleted(qm.ActiveQuestId);
            if (!needsNewQuest)
            {
                HideQuestNotification();
                return;
            }

            bool hasAvailable = false;
            var quests = qm.AllQuests;
            for (int i = 0; i < quests.Count; i++)
            {
                if (qm.IsQuestCompleted(quests[i].questId)) continue;
                if (qm.IsQuestUnlocked(quests[i].questId))
                {
                    hasAvailable = true;
                    break;
                }
            }

            if (hasAvailable)
                ShowQuestNotification();
            else
                HideQuestNotification();
        }

        private void ShowQuestNotification()
        {
            if (questNotifyObj == null || questNotifyObj.activeSelf) return;

            if (questNotifyRect == null)
                questNotifyRect = questNotifyObj.GetComponent<RectTransform>();

            questNotifyObj.SetActive(true);
            questNotifyStartY = questNotifyRect.anchoredPosition.y;
            questNotifyRect.localScale = Vector3.zero;
            DOTween.Kill(questNotifyRect);

            // Pop in
            questNotifyRect.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);

            // Repeating jump + bounce
            var seq = DOTween.Sequence().SetUpdate(true).SetDelay(0.4f).SetLoops(-1, LoopType.Restart);
            seq.Append(questNotifyRect.DOAnchorPosY(questNotifyStartY + 20f, 0.25f).SetEase(Ease.OutQuad));
            seq.Append(questNotifyRect.DOAnchorPosY(questNotifyStartY, 0.2f).SetEase(Ease.OutBounce));
            seq.AppendInterval(0.8f);
            seq.SetTarget(questNotifyRect);
        }

        private void HideQuestNotification()
        {
            if (questNotifyObj == null || !questNotifyObj.activeSelf) return;

            if (questNotifyRect == null)
                questNotifyRect = questNotifyObj.GetComponent<RectTransform>();

            DOTween.Kill(questNotifyRect);
            questNotifyRect.anchoredPosition = new Vector2(questNotifyRect.anchoredPosition.x, questNotifyStartY);
            questNotifyObj.SetActive(false);
        }

        private void OnContinuousClicked()
        {
            if (labTutorialActive) return;
            if (questTutorialActive && questTutorialStep != 2) return;
            if (questTutorialActive && questTutorialStep == 2)
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
                case 0: return upgrade.upgradeType != LabUpgradeType.TowerUnlock
                    && upgrade.upgradeType != LabUpgradeType.ModUnlock
                    && upgrade.upgradeType != LabUpgradeType.PieceUnlock;
                case 1: return upgrade.upgradeType == LabUpgradeType.TowerUnlock;
                case 2: return upgrade.upgradeType == LabUpgradeType.ModUnlock;
                case 3: return upgrade.upgradeType == LabUpgradeType.PieceUnlock;
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
            bool isUnlock = upgrade.upgradeType == LabUpgradeType.TowerUnlock || upgrade.upgradeType == LabUpgradeType.ModUnlock;

            var card = Instantiate(labUpgradePrefab, labGridContent);
            card.name = $"Card_{upgrade.upgradeName}";
            var ui = card.GetComponent<LabUpgradeUI>();

            // Accent color per resource
            ui.Accent.color = GetResourceColor(upgrade.costResource);

            // Background
            ui.Background.color = maxed
                ? new Color(0.12f, 0.18f, 0.12f, 0.9f)
                : new Color(0.14f, 0.14f, 0.2f, 0.9f);

            // Name
            ui.NameLabel.text = upgrade.upgradeName;

            // Description
            ui.DescriptionLabel.text = upgrade.description;

            // Level / status
            if (isUnlock)
            {
                ui.LevelLabel.text = maxed ? "UNLOCKED" : "LOCKED";
                ui.LevelLabel.color = maxed ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.8f, 0.4f, 0.4f);
            }
            else
            {
                ui.LevelLabel.text = $"Lv {level}/{upgrade.maxLevel}";
                ui.LevelLabel.color = maxed ? new Color(0.4f, 0.8f, 0.4f) : Color.white;
            }

            // Buy button
            if (!maxed)
            {
                int cost = upgrade.GetCost(level);
                ui.BuyButtonBg.color = canBuy ? new Color(0.2f, 0.5f, 0.2f) : new Color(0.3f, 0.3f, 0.3f);
                ui.BuyButton.interactable = canBuy;
                ui.BuyButtonText.text = $"Buy {cost} {GetResourceShortName(upgrade.costResource)}";
                ui.BuyButtonText.color = canBuy ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                var capturedUpgrade = upgrade;
                ui.BuyButton.onClick.AddListener(() => OnBuyUpgrade(capturedUpgrade));
            }
            else
            {
                ui.BuyButtonBg.color = new Color(0.25f, 0.25f, 0.25f);
                ui.BuyButton.interactable = false;
                ui.BuyButtonText.text = "MAX";
                ui.BuyButtonText.color = new Color(1f, 0.85f, 0.2f);
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
            HideQuestNotification();
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
            UpdateQuestStartButton();
            if (questPanel != null) questPanel.SetActive(true);
        }

        private void UpdateQuestStartButton()
        {
            if (questStartButton == null) return;
            bool hasQuest = QuestManager.Instance != null && QuestManager.Instance.HasActiveQuest;
            questStartButton.interactable = hasQuest;
        }

        private void OnQuestBackClicked()
        {
            if (questTutorialActive) return;
            if (questPanel != null) questPanel.SetActive(false);
            ShowMap();
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

            var qm = QuestManager.Instance;
            var quests = qm.AllQuests;
            for (int i = 0; i < quests.Count; i++)
            {
                if (!qm.IsQuestUnlocked(quests[i].questId))
                    continue;
                // Hide completed quests that are not repeatable
                if (qm.IsQuestCompleted(quests[i].questId) && !quests[i].repeatable)
                    continue;
                questCards.Add(CreateQuestCard(quests[i]));
            }
        }

        private GameObject CreateQuestCard(QuestDefinition quest)
        {
            var qm = QuestManager.Instance;
            bool isActive = qm.ActiveQuestId == quest.questId;
            bool isCompleted = qm.IsQuestCompleted(quest.questId);

            var card = Instantiate(questCardPrefab, questGridContent);
            card.name = $"QuestCard_{quest.questId}";
            var ui = card.GetComponent<QuestCardUI>();

            // Name
            ui.NameLabel.text = quest.questName;

            // Quest image
            if (ui.QuestImage != null)
            {
                if (quest.questImage != null)
                {
                    ui.QuestImage.sprite = quest.questImage;
                    ui.QuestImage.enabled = true;
                }
                else
                {
                    ui.QuestImage.enabled = false;
                }
            }

            // Selected indicator
            if (ui.SelectedImage != null)
                ui.SelectedImage.SetActive(isActive);

            // Whole card tappable
            var capturedQuest = quest;
            ui.CardButton.onClick.AddListener(() => ShowQuestDetail(capturedQuest));

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

            // Advance tutorial before closing detail so CloseQuestDetail's step-1 guard doesn't re-trigger
            if (questTutorialActive && questTutorialStep == 1)
                questTutorialStep = 2;

            CloseQuestDetail();
            RefreshQuestCards();
            UpdateQuestStartButton();

            if (questTutorialActive && questTutorialStep == 2)
                ShowQuestTutorialStep();
        }

        private void ShowQuestDetail(QuestDefinition quest)
        {
            CloseQuestDetail();

            var qm = QuestManager.Instance;
            if (qm == null || questDetailPrefab == null) return;

            bool isActive = qm.ActiveQuestId == quest.questId;
            bool isCompleted = qm.IsQuestCompleted(quest.questId);

            var parent = questPanel != null ? questPanel.transform.parent : transform;

            questDetailOverlay = Instantiate(questDetailPrefab, parent);
            questDetailOverlay.name = "QuestDetailOverlay";

            // Background tap closes the popup
            var bgBtn = questDetailOverlay.GetComponent<Button>();
            if (bgBtn != null)
                bgBtn.onClick.AddListener(CloseQuestDetail);

            var dialog = questDetailOverlay.transform.Find("Dialog");
            if (dialog == null) return;

            // Title
            var titleTMP = dialog.Find("Title")?.GetComponent<TextMeshProUGUI>();
            if (titleTMP != null)
                titleTMP.text = quest.questName;

            // Description
            var descTMP = dialog.Find("Description")?.GetComponent<TextMeshProUGUI>();
            if (descTMP != null)
                descTMP.text = quest.description;

            // Objectives
            var objTMP = dialog.Find("Objectives")?.GetComponent<TextMeshProUGUI>();
            if (objTMP != null)
                objTMP.text = "Objectives:\n" + BuildObjectiveSummary(quest);

            // Reward
            var rewardTMP = dialog.Find("Reward")?.GetComponent<TextMeshProUGUI>();
            if (rewardTMP != null)
            {
                string rewardStr = quest.rewardAmount > 0
                    ? $"Reward: {quest.rewardAmount} {GetResourceShortName(quest.rewardResource)}"
                    : "Reward:";
                if (!string.IsNullOrEmpty(quest.unlockLabUpgrade))
                    rewardStr += (quest.rewardAmount > 0 ? " + " : " ") + $"Unlock {quest.unlockLabUpgrade}";
                rewardTMP.text = rewardStr;
            }

            // Accept button
            var acceptBtnTransform = dialog.Find("AcceptBtn");
            if (acceptBtnTransform != null)
            {
                var acceptBtn = acceptBtnTransform.GetComponent<Button>();
                var acceptImg = acceptBtnTransform.GetComponent<Image>();
                var acceptText = acceptBtnTransform.GetComponentInChildren<TextMeshProUGUI>();

                if (isActive)
                {
                    if (acceptImg != null) acceptImg.color = new Color(0.4f, 0.4f, 0.3f);
                    if (acceptText != null) acceptText.text = "ACTIVE";
                    if (acceptBtn != null) acceptBtn.interactable = false;
                }
                else if (isCompleted && quest.repeatable)
                {
                    if (acceptImg != null) acceptImg.color = new Color(0.2f, 0.5f, 0.2f);
                    if (acceptText != null) acceptText.text = "Replay";
                    var capturedId = quest.questId;
                    if (acceptBtn != null) acceptBtn.onClick.AddListener(() => OnAcceptQuest(capturedId));
                }
                else
                {
                    if (acceptImg != null) acceptImg.color = new Color(0.2f, 0.4f, 0.6f);
                    if (acceptText != null) acceptText.text = "Accept";
                    var capturedId = quest.questId;
                    if (acceptBtn != null) acceptBtn.onClick.AddListener(() => OnAcceptQuest(capturedId));
                }

                questDetailAcceptBtnRect = acceptBtnTransform.GetComponent<RectTransform>();
            }

            // Close button
            var closeBtnTransform = dialog.Find("CloseBtn");
            if (closeBtnTransform != null)
            {
                var closeBtn = closeBtnTransform.GetComponent<Button>();
                if (closeBtn != null)
                    closeBtn.onClick.AddListener(CloseQuestDetail);
            }

            // Tutorial: if quest panel tutorial is active at step 1, point at accept button
            if (questTutorialActive && questTutorialStep == 1)
                ShowQuestTutorialStep();
        }

        private void CloseQuestDetail()
        {
            if (questDetailOverlay != null)
            {
                Destroy(questDetailOverlay);
                questDetailOverlay = null;
            }
            questDetailAcceptBtnRect = null;

            // Re-point tutorial arrow back at quest cards when detail is closed without accepting
            if (questTutorialActive && questTutorialStep == 1)
                ShowQuestTutorialStep();
        }

        // --- Static helper ---

        public static void LoadMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }
    }
}
