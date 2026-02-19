using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using DG.Tweening;
using TowerDefense.Core;
using TowerDefense.Grid;
using TowerDefense.Entities;
using TowerDefense.Data;


namespace TowerDefense.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("HUD Canvas")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;

        [Header("Lives Bar")]
        [SerializeField] private Image livesBarFill;
        [SerializeField] private TextMeshProUGUI livesBarText;

        [Header("Resources")]
        [SerializeField] private TextMeshProUGUI currencyText;

        [SerializeField] private TextMeshProUGUI ironOreText;
        [SerializeField] private Image ironOreIcon;
        [SerializeField] private GameObject ironOreRow;
        [SerializeField] private TextMeshProUGUI gemsText;
        [SerializeField] private Image gemsIcon;
        [SerializeField] private GameObject gemsRow;
        [SerializeField] private TextMeshProUGUI florpusText;
        [SerializeField] private Image florpusIcon;
        [SerializeField] private GameObject florpusRow;
        [SerializeField] private TextMeshProUGUI adamantiteText;
        [SerializeField] private Image adamantiteIcon;
        [SerializeField] private GameObject adamantiteRow;

        [Header("Buttons")]
        [SerializeField] private Button startWaveButton;
        [SerializeField] private GameObject exitRunButtonObj;
        [SerializeField] private Button upgradesButton;
        [SerializeField] private Image upgradesButtonImage;
        [SerializeField] private Button cheatToggleButton;

        [Header("Panels")]
        [SerializeField] private GameObject towerPanel;
        [SerializeField] private GameObject towerInfoPanel;
        [SerializeField] private GameObject cheatPanelObj;

        [Header("Build Timer")]
        [SerializeField] private TextMeshProUGUI buildTimerText;
        [SerializeField] private GameObject buildTimerObj;

        [Header("Escape")]
        [SerializeField] private Button escapeButton;
        [SerializeField] private GameObject escapeButtonObj;
        [SerializeField] private TextMeshProUGUI escapeButtonText;
        [SerializeField] private GameObject escapeConfirmOverlay;
        [SerializeField] private GameObject escapeOverlayCanvasObj;

        [Header("Quests")]
        [SerializeField] private Button questsButton;

        private int lastLives = -1;
        private Dictionary<ResourceType, TextMeshProUGUI> resourceTexts = new Dictionary<ResourceType, TextMeshProUGUI>();
        private Dictionary<ResourceType, Image> resourceIcons = new Dictionary<ResourceType, Image>();
        private Dictionary<ResourceType, GameObject> resourceRows = new Dictionary<ResourceType, GameObject>();
        private List<GameObject> towerButtons = new List<GameObject>();

        private WaveManager waveManager;
        private TowerManager towerManager;
        private UpgradeSelectionUI upgradeSelectionUI;
        private List<GameObject> cheatSpawnerMarkers = new List<GameObject>();
        private bool showingSpawners;

        // Continuous mode escape
        private bool continuousStarted;
        private bool escapeAvailable;

        // Quest escape tutorial
        private bool questEscapeTutActive;
        private GameObject tutPanelObj;
        private GameObject tutArrowObj;
        private RectTransform tutArrowRect;

        // Extraction countdown
        private GameObject extractionPopup;
        private TextMeshProUGUI extractionTimerText;
        private int lastExtractionSeconds = -1;

        // Objectives popup
        private GameObject objectivesPopup;
        private Tween questsButtonTween;
        // Upgrade button glow
        private bool canAffordUpgrade;
        private float upgradeCheckTimer;
        private static readonly Color NormalUpgradeColor = new Color(0.3f, 0.2f, 0.6f);

        // Particle glow for upgrade button
        private ParticleSystem upgradeGlowPS;
        private Camera cachedCamera;

        // Damage flash overlay
        private Image damageFlashImage;
        private float damageFlashTimer;

        // Cached values for throttling UI text updates
        private int lastBuildSeconds = -1;

        private void Awake()
        {
            // Wire button events
            if (startWaveButton != null) startWaveButton.onClick.AddListener(OnStartWaveClicked);
            if (upgradesButton != null) upgradesButton.onClick.AddListener(OnUpgradesClicked);
            if (escapeButton != null) escapeButton.onClick.AddListener(OnEscapeClicked);
            if (questsButton != null)
            {
                questsButton.onClick.AddListener(OnQuestsClicked);
                questsButton.gameObject.SetActive(false);
            }
            if (cheatToggleButton != null) cheatToggleButton.onClick.AddListener(OnCheatToggle);

            // Only wire exit-run if it's a separate button from escape
            if (exitRunButtonObj != null && exitRunButtonObj != escapeButtonObj)
                WireButton(exitRunButtonObj, OnExitRunClicked);
            WireChildButton(towerInfoPanel, "SellButton", OnSellClicked);
            WireChildButton(cheatPanelObj, "CheatGold", OnCheatGold);
            WireChildButton(cheatPanelObj, "CheatResources", OnCheatResources);
            WireChildButton(cheatPanelObj, "CheatShowCamps", OnCheatShowCamps);
            WireChildButton(cheatPanelObj, "CheatUnlockTowers", OnCheatUnlockTowers);
            WireChildButton(cheatPanelObj, "CheatReset", OnCheatResetProgress);
            WireChildButton(cheatPanelObj, "CheatForceEscape", OnCheatForceEscape);
            WireChildButton(escapeConfirmOverlay, "Panel/YesBtn", OnEscapeConfirmed);
            WireChildButton(escapeConfirmOverlay, "Panel/NoBtn", OnEscapeCancelled);
        }

        private void Start()
        {
            // Populate resource dictionaries from SerializeField references
            if (ironOreText != null)
            {
                resourceTexts[ResourceType.IronOre] = ironOreText;
                resourceIcons[ResourceType.IronOre] = ironOreIcon;
                resourceRows[ResourceType.IronOre] = ironOreRow;
            }
            if (gemsText != null)
            {
                resourceTexts[ResourceType.Gems] = gemsText;
                resourceIcons[ResourceType.Gems] = gemsIcon;
                resourceRows[ResourceType.Gems] = gemsRow;
            }
            if (florpusText != null)
            {
                resourceTexts[ResourceType.Florpus] = florpusText;
                resourceIcons[ResourceType.Florpus] = florpusIcon;
                resourceRows[ResourceType.Florpus] = florpusRow;
            }
            if (adamantiteText != null)
            {
                resourceTexts[ResourceType.Adamantite] = adamantiteText;
                resourceIcons[ResourceType.Adamantite] = adamantiteIcon;
                resourceRows[ResourceType.Adamantite] = adamantiteRow;
            }

            waveManager = FindFirstObjectByType<WaveManager>();
            towerManager = FindFirstObjectByType<TowerManager>();
            upgradeSelectionUI = FindFirstObjectByType<UpgradeSelectionUI>();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLivesChanged += UpdateLives;
                GameManager.Instance.OnCurrencyChanged += UpdateCurrency;
                GameManager.Instance.OnGameOver += ShowGameOver;
                GameManager.Instance.OnBuildPhaseStarted += OnBuildPhaseStarted;
                GameManager.Instance.OnBuildPhaseEnded += OnBuildPhaseEnded;

                UpdateLives(GameManager.Instance.Lives);
                UpdateCurrency(GameManager.Instance.Currency);

                // Set resource icons now that GameManager is available
                foreach (var kvp in resourceIcons)
                {
                    var sprite = GameManager.Instance.GetResourceSprite(kvp.Key);
                    if (sprite != null)
                        kvp.Value.sprite = sprite;
                    kvp.Value.color = GameManager.Instance.GetResourceColor(kvp.Key);
                }
            }

            if (towerManager != null)
            {
                if (!GameManager.Instance.UseFreeTowerPlacement)
                    towerManager.OnSlotSelected += ShowTowerPanel;
                towerManager.OnTowerSelected += ShowTowerInfo;
                towerManager.OnSelectionCleared += HidePanels;
            }

            if (waveManager != null)
            {
                waveManager.OnWaveComplete += ShowExitRunButton;
            }

            if (PersistenceManager.Instance != null)
            {
                PersistenceManager.Instance.OnResourcesChanged += UpdateResources;
                UpdateResources();
            }

            if (QuestManager.Instance != null)
                QuestManager.Instance.OnQuestProgressChanged += UpdateEscapeProgress;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnObjectivesMet += OnObjectivesMet;
                GameManager.Instance.OnExtractionTick += OnExtractionTick;
                GameManager.Instance.OnExtractionComplete += OnExtractionComplete;
            }

            cachedCamera = Camera.main;
            CreateUpgradeGlowParticles();
            CreateDamageFlashOverlay();
        }

        private static void WireButton(GameObject obj, UnityEngine.Events.UnityAction action)
        {
            if (obj == null) return;
            var btn = obj.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(action);
        }

        private static void WireChildButton(GameObject parent, string path, UnityEngine.Events.UnityAction action)
        {
            if (parent == null) return;
            var t = parent.transform.Find(path);
            if (t == null) return;
            var btn = t.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(action);
        }

        private Button CreateButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent, false);

            var rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 0.2f);

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            // Button text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 20;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            return button;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, string content)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent);

            var rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(150, 40);

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            return text;
        }

        private void UpdateLives(int lives)
        {
            if (lives == lastLives) return;
            lastLives = lives;

            if (livesBarFill != null && GameManager.Instance != null)
            {
                int max = GameManager.Instance.MaxLives;
                float fraction = max > 0 ? Mathf.Clamp01((float)lives / max) : 0f;
                livesBarFill.fillAmount = fraction;

                // Color shifts from green to red as health drops
                livesBarFill.color = Color.Lerp(new Color(0.8f, 0.15f, 0.15f), new Color(0.2f, 0.75f, 0.2f), fraction);
            }
            if (livesBarText != null)
                livesBarText.text = $"{lives}";
        }

        private void UpdateCurrency(int currency)
        {
            if (currencyText != null)
                currencyText.text = $"{currency}";
        }

        private TowerSlot currentSlot;

        private void ShowTowerPanel(TowerSlot slot)
        {
            currentSlot = slot;
            towerPanel.SetActive(true);
            towerInfoPanel.SetActive(false);
            PopulateTowerButtons();
        }

        private void PopulateTowerButtons()
        {
            // Clear existing buttons
            foreach (var btn in towerButtons)
            {
                if (btn != null) Destroy(btn);
            }
            towerButtons.Clear();

            if (towerManager == null || towerManager.AvailableTowers == null)
                return;

            var towers = towerManager.AvailableTowers;
            float buttonWidth = 100f;
            float spacing = 10f;
            float totalWidth = towers.Count * buttonWidth + (towers.Count - 1) * spacing;
            float startX = -totalWidth / 2f + buttonWidth / 2f;

            for (int i = 0; i < towers.Count; i++)
            {
                var towerData = towers[i];
                float xPos = startX + i * (buttonWidth + spacing);

                GameObject buttonObj = new GameObject($"TowerBtn_{towerData.towerName}");
                buttonObj.transform.SetParent(towerPanel.transform);

                var rect = buttonObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(xPos, 0);
                rect.sizeDelta = new Vector2(buttonWidth, 60);

                var image = buttonObj.AddComponent<Image>();
                image.color = towerData.towerColor;

                var button = buttonObj.AddComponent<Button>();
                button.targetGraphic = image;

                // Capture towerData for closure
                var data = towerData;
                button.onClick.AddListener(() => OnTowerButtonClicked(data));

                // Button text (name + cost)
                var textObj = new GameObject("Text");
                textObj.transform.SetParent(buttonObj.transform);
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var text = textObj.AddComponent<TextMeshProUGUI>();
                text.text = $"{towerData.towerName}\n{towerData.cost}g";
                text.fontSize = 14;
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.Center;

                towerButtons.Add(buttonObj);
            }

        }

        private void OnTowerButtonClicked(TowerData towerData)
        {
            if (towerManager != null)
            {
                bool success = towerManager.BuildTower(towerData);
                if (success)
                {
                    Debug.Log($"Built tower: {towerData.towerName}");
                }
                else
                {
                    Debug.Log($"Failed to build tower: {towerData.towerName} (not enough gold or no slot selected)");
                }
            }
        }

        private void ShowTowerInfo(Entities.Tower tower)
        {
            towerPanel.SetActive(false);
            towerInfoPanel.SetActive(true);
        }

        private void HidePanels()
        {
            towerPanel.SetActive(false);
            towerInfoPanel.SetActive(false);
        }

        private void ShowGameOver()
        {
            HideExtractionPopup(); // Clean up if died during extraction

            // Only show game over screen on death, not voluntary escape
            if (GameManager.Instance != null && GameManager.Instance.Lives > 0) return;

            CreateText(canvas.transform, "GameOverText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 30), "GAME OVER");

            CreateButton(canvas.transform, "ReturnBtn", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -40), new Vector2(200, 50), "Return to Menu", () => MainSceneController.LoadMainMenu());
        }

        private void ShowExitRunButton()
        {
            if (exitRunButtonObj != null)
                exitRunButtonObj.SetActive(true);
        }

        private void OnStartWaveClicked()
        {
            Debug.Log($"HUD: Start Wave button clicked. WaveManager: {(waveManager != null ? "Found" : "NULL")}");
            if (GameManager.Instance != null && GameManager.Instance.BuildPhaseActive)
            {
                GameManager.Instance.SkipBuildPhase();
            }
            else if (waveManager != null && waveManager.IsContinuousMode && !continuousStarted)
            {
                waveManager.StartContinuousMode();
                continuousStarted = true;
                escapeAvailable = false;
                if (startWaveButton != null)
                    startWaveButton.gameObject.SetActive(false);
                if (exitRunButtonObj != null)
                    exitRunButtonObj.SetActive(false);
                if (escapeButtonObj != null)
                {
                    escapeButtonObj.SetActive(true);
                    escapeButton.interactable = false; // Enabled when objectives met
                }
                if (questsButton != null)
                    questsButton.gameObject.SetActive(true);
                UpdateEscapeProgress();
            }
            else if (waveManager != null)
            {
                if (exitRunButtonObj != null)
                    exitRunButtonObj.SetActive(false);

                waveManager.StartWave();
            }
            else
            {
                Debug.LogError("HUD: WaveManager is null! Cannot start wave.");
            }
        }

        private void OnExitRunClicked()
        {
            // Don't exit if escape confirm dialog is active
            if (escapeConfirmOverlay != null && escapeConfirmOverlay.activeSelf) return;
            GameManager.Instance?.ExitRun();
        }

        private void OnBuildPhaseStarted()
        {
            if (buildTimerObj != null)
                buildTimerObj.SetActive(true);
            if (startWaveButton != null)
            {
                startWaveButton.gameObject.SetActive(true);
                SetButtonLabel(startWaveButton, "Start Now");
            }
            if (exitRunButtonObj != null)
                exitRunButtonObj.SetActive(true);
        }

        private void OnBuildPhaseEnded()
        {
            if (buildTimerObj != null)
                buildTimerObj.SetActive(false);
            if (startWaveButton != null)
                SetButtonLabel(startWaveButton, "Start Wave");
            if (exitRunButtonObj != null)
                exitRunButtonObj.SetActive(false);
        }

        private void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = label;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.BuildPhaseActive && buildTimerText != null)
            {
                int seconds = Mathf.CeilToInt(GameManager.Instance.BuildTimer);
                if (seconds != lastBuildSeconds)
                {
                    lastBuildSeconds = seconds;
                    buildTimerText.text = $"Build phase: {seconds}s";
                }
            }

            // Upgrade button glow (considers banked + run-gathered resources)
            upgradeCheckTimer -= Time.unscaledDeltaTime;
            if (upgradeCheckTimer <= 0f)
            {
                upgradeCheckTimer = 1f;
                canAffordUpgrade = false;
                if (LabManager.Instance != null && PersistenceManager.Instance != null)
                {
                    var upgrades = LabManager.Instance.Upgrades;
                    var pm = PersistenceManager.Instance;
                    for (int i = 0; i < upgrades.Count; i++)
                    {
                        var upgrade = upgrades[i];
                        int level = LabManager.Instance.GetLevel(upgrade);
                        if (level >= upgrade.maxLevel) continue;
                        int cost = upgrade.GetCost(level);
                        int available = pm.GetBanked(upgrade.costResource) + pm.GetRunGathered(upgrade.costResource);
                        if (available >= cost)
                        {
                            canAffordUpgrade = true;
                            break;
                        }
                    }
                }
            }
            if (upgradeGlowPS != null)
            {
                if (canAffordUpgrade)
                {
                    if (!upgradeGlowPS.gameObject.activeSelf)
                    {
                        upgradeGlowPS.gameObject.SetActive(true);
                        upgradeGlowPS.Play();
                    }
                    // Position particle system behind the button via camera projection
                    if (cachedCamera != null && upgradesButton != null)
                    {
                        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, upgradesButton.transform.position);
                        Ray ray = cachedCamera.ScreenPointToRay(screenPos);
                        upgradeGlowPS.transform.position = ray.GetPoint(30f);
                    }
                }
                else if (upgradeGlowPS.gameObject.activeSelf)
                {
                    upgradeGlowPS.Stop();
                    upgradeGlowPS.gameObject.SetActive(false);
                }
            }

            // Damage flash fade
            if (damageFlashTimer > 0f && damageFlashImage != null)
            {
                damageFlashTimer -= Time.deltaTime;
                float alpha = Mathf.Max(0f, damageFlashTimer / 0.35f) * 0.35f;
                damageFlashImage.color = new Color(1f, 0f, 0f, alpha);
            }

            // Escape progress is updated via OnResourcesChanged / OnObjectivesMet events

            if (questEscapeTutActive)
                UpdateQuestEscapeTutArrow();
        }

        private void UpdateResources()
        {
            if (resourceTexts.Count == 0 || PersistenceManager.Instance == null) return;

            var pm = PersistenceManager.Instance;
            var names = new Dictionary<ResourceType, string>
            {
                { ResourceType.IronOre, "Iron" },
                { ResourceType.Gems, "Gems" },
                { ResourceType.Florpus, "Florpus" },
                { ResourceType.Adamantite, "Adam" }
            };

            foreach (var kvp in resourceTexts)
            {
                var resType = kvp.Key;
                var text = kvp.Value;
                if (text == null) continue;
                string name = names.TryGetValue(resType, out var n) ? n : resType.ToString();
                int banked = pm.GetBanked(resType);
                int runGathered = pm.GetRunGathered(resType);
                text.text = $"{name}: {banked}(+{runGathered})";

                // Hide rows with no resources
                if (resourceRows.TryGetValue(resType, out var row) && row != null)
                    row.SetActive(banked + runGathered > 0);
            }
        }

        private void OnCheatToggle()
        {
            if (cheatPanelObj != null)
                cheatPanelObj.SetActive(!cheatPanelObj.activeSelf);
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) JsonSaveSystem.SaveIfDirty();
        }

        private void OnApplicationQuit()
        {
            JsonSaveSystem.SaveIfDirty();
        }

        private void OnCheatGold()
        {
            GameManager.Instance?.AddCurrency(1000);
        }

        private void OnCheatResources()
        {
            if (PersistenceManager.Instance == null) return;
            PersistenceManager.Instance.AddRunResource(ResourceType.IronOre, 100);
            PersistenceManager.Instance.AddRunResource(ResourceType.Gems, 100);
            PersistenceManager.Instance.AddRunResource(ResourceType.Florpus, 100);
            PersistenceManager.Instance.AddRunResource(ResourceType.Adamantite, 100);
        }

        private void OnCheatUnlockTowers()
        {
            if (LabManager.Instance == null) return;
            LabManager.Instance.UnlockAllTowers();

            // Refresh tower list in hand UI
            if (towerManager != null && GameManager.Instance != null && GameManager.Instance.UseFreeTowerPlacement)
            {
                var handUI = FindFirstObjectByType<PieceHandUI>();
                if (handUI != null)
                {
                    handUI.SetAvailableTowers(towerManager.AllTowers);
                    handUI.RefreshTowers();
                }
            }
        }

        private void OnCheatResetProgress()
        {
            JsonSaveSystem.DeleteAll();
            MainSceneController.LoadMainMenu();
        }

        private void UpdateEscapeProgress()
        {
            if (!continuousStarted || escapeAvailable) return;
            if (escapeButtonText == null) return;

            escapeButtonText.text = "objectives missing";
            escapeButtonText.fontSize = 30;

            StartQuestsButtonPulse();
        }

        private static string GetObjectiveLabel(QuestObjective obj)
        {
            switch (obj.objectiveType)
            {
                case QuestObjectiveType.GatherResource:
                    return GetShortResourceName(obj.resourceType);
                case QuestObjectiveType.KillEnemies:
                    return "Kills";
                case QuestObjectiveType.ExpandTiles:
                    return "Tiles";
                default:
                    return "???";
            }
        }

        private void OnObjectivesMet()
        {
            escapeAvailable = true;
            if (escapeButton != null) escapeButton.interactable = true;
            if (escapeButtonObj != null)
                escapeButtonObj.GetComponent<Image>().color = new Color(0.8f, 0.65f, 0.1f);
            if (escapeButtonText != null)
            {
                escapeButtonText.text = "Escape!";
                escapeButtonText.fontSize = 50;
            }
            StopQuestsButtonPulse();

            if (!JsonSaveSystem.Data.questEscapeTutComplete)
                StartQuestEscapeTutorial();
        }

        private void StartQuestsButtonPulse()
        {
            if (questsButton == null || questsButtonTween != null) return;
            questsButtonTween = questsButton.transform
                .DOScale(1.08f, 1.2f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }

        private void StopQuestsButtonPulse()
        {
            if (questsButtonTween != null)
            {
                questsButtonTween.Kill();
                questsButtonTween = null;
            }
            if (questsButton != null)
                questsButton.transform.localScale = Vector3.one;
        }

        private void OnQuestsClicked()
        {
            if (objectivesPopup != null) return;
            ShowObjectivesPopup();
        }

        private void ShowObjectivesPopup()
        {
            var qm = QuestManager.Instance;
            if (qm == null || !qm.HasActiveQuest) return;
            var quest = qm.GetActiveQuest();
            if (quest == null) return;

            Time.timeScale = 0f;

            // Overlay background (dim + catches taps to close)
            objectivesPopup = new GameObject("ObjectivesPopup");
            objectivesPopup.transform.SetParent(canvas.transform, false);
            var overlayRect = objectivesPopup.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImg = objectivesPopup.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.6f);
            var overlayBtn = objectivesPopup.AddComponent<Button>();
            overlayBtn.onClick.AddListener(CloseObjectivesPopup);

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(objectivesPopup.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700f, 400f);
            panelRect.anchoredPosition = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            panelImg.raycastTarget = true;

            // Title
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -16f);
            titleRect.sizeDelta = new Vector2(0f, 50f);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Objectives";
            titleText.fontSize = 44;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;

            // Objectives list
            var listObj = new GameObject("ObjectivesList");
            listObj.transform.SetParent(panel.transform, false);
            var listRect = listObj.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.offsetMin = new Vector2(40f, 60f);
            listRect.offsetMax = new Vector2(-40f, -80f);
            var listText = listObj.AddComponent<TextMeshProUGUI>();
            listText.fontSize = 40;
            listText.color = Color.white;
            listText.alignment = TextAlignmentOptions.Center;
            listText.raycastTarget = false;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var obj = quest.objectives[i];
                int current = qm.GetObjectiveProgress(obj);
                int required = obj.requiredAmount;
                string label = GetObjectiveLabel(obj);
                bool met = current >= required;
                string color = met ? "#88FF88" : "#FFFFFF";
                if (i > 0) sb.Append("\n");
                sb.Append($"<color={color}>{label}: {current}/{required}</color>");
            }
            listText.text = sb.ToString();

            // Close button
            var closeObj = new GameObject("CloseBtn");
            closeObj.transform.SetParent(panel.transform, false);
            var closeRect = closeObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 12f);
            closeRect.sizeDelta = new Vector2(200f, 50f);
            var closeBtnImg = closeObj.AddComponent<Image>();
            closeBtnImg.color = new Color(0.3f, 0.3f, 0.4f);
            var closeBtn = closeObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;
            closeBtn.onClick.AddListener(CloseObjectivesPopup);

            var closeTxtObj = new GameObject("Text");
            closeTxtObj.transform.SetParent(closeObj.transform, false);
            var closeTxtRect = closeTxtObj.AddComponent<RectTransform>();
            closeTxtRect.anchorMin = Vector2.zero;
            closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.offsetMin = Vector2.zero;
            closeTxtRect.offsetMax = Vector2.zero;
            var closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
            closeTxt.text = "Close";
            closeTxt.fontSize = 28;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAlignmentOptions.Center;
        }

        private void CloseObjectivesPopup()
        {
            if (objectivesPopup != null)
            {
                Destroy(objectivesPopup);
                objectivesPopup = null;
            }
            Time.timeScale = 1f;
        }

        private void StartQuestEscapeTutorial()
        {
            questEscapeTutActive = true;
            Time.timeScale = 0f;

            // Panel
            tutPanelObj = new GameObject("QuestEscapeTutPanel");
            tutPanelObj.transform.SetParent(canvas.transform, false);
            var panelRect = tutPanelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 60f);
            panelRect.sizeDelta = new Vector2(640f, 120f);
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
            var msgText = textObj.AddComponent<TextMeshProUGUI>();
            msgText.fontSize = 20;
            msgText.fontStyle = FontStyles.Bold;
            msgText.color = Color.white;
            msgText.alignment = TextAlignmentOptions.Center;
            msgText.raycastTarget = false;
            msgText.text = "Quest complete! Tap Escape to leave with your rewards.\nNext time you can choose to stay and keep exploring.";

            // Arrow — reuse the same sprite from the base tutorial
            tutArrowObj = new GameObject("QuestEscapeTutArrow");
            tutArrowObj.transform.SetParent(canvas.transform, false);
            tutArrowRect = tutArrowObj.AddComponent<RectTransform>();
            tutArrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            tutArrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            tutArrowRect.sizeDelta = new Vector2(60f, 60f);
            var arrowImg = tutArrowObj.AddComponent<Image>();
            if (TutorialManager.CachedArrowSprite != null)
                arrowImg.sprite = TutorialManager.CachedArrowSprite;
            arrowImg.color = new Color(1f, 0.3f, 0.3f, 1f);
            arrowImg.raycastTarget = false;

            // Rotate arrow to point downward at the escape button
            tutArrowRect.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }

        private void UpdateQuestEscapeTutArrow()
        {
            if (escapeButtonObj == null || tutArrowRect == null) return;

            var targetRect = escapeButtonObj.GetComponent<RectTransform>();
            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, targetRect.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPos, null, out Vector2 localPoint))
            {
                float bounce = Mathf.Sin(Time.unscaledTime * 3f) * 15f;
                Vector2 offset = new Vector2(0f, 1f) * (50f + bounce);
                tutArrowRect.anchoredPosition = localPoint + offset;
            }
        }

        private void CompleteQuestEscapeTutorial()
        {
            JsonSaveSystem.Data.questEscapeTutComplete = true;
            JsonSaveSystem.Save();
            if (tutPanelObj != null) Destroy(tutPanelObj);
            if (tutArrowObj != null) Destroy(tutArrowObj);
            questEscapeTutActive = false;
            Time.timeScale = 1f;
        }

        private static string GetShortResourceName(ResourceType type)
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

        private void OnCheatForceEscape()
        {
            if (!continuousStarted) return;
            OnObjectivesMet();
        }

        private void OnCheatShowCamps()
        {
            showingSpawners = !showingSpawners;

            if (!showingSpawners)
            {
                foreach (var marker in cheatSpawnerMarkers)
                    if (marker != null) Destroy(marker);
                cheatSpawnerMarkers.Clear();
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null) return;

            foreach (var coord in gm.HiddenSpawners)
            {
                var worldPos = HexGrid.HexToWorld(coord);
                var marker = new GameObject("CheatSpawnerMarker");
                marker.transform.position = worldPos + Vector3.up * 8f;
                marker.transform.localScale = Vector3.one * 2f;

                var sr = marker.AddComponent<SpriteRenderer>();
                if (gm.GoblinCampSprite != null)
                    sr.sprite = gm.GoblinCampSprite;
                sr.color = new Color(1f, 0.3f, 0.3f);

                marker.AddComponent<BillboardSprite>();
                cheatSpawnerMarkers.Add(marker);
            }
        }

        private void OnEscapeClicked()
        {
            if (questEscapeTutActive)
            {
                CompleteQuestEscapeTutorial();
                GameManager.Instance?.ExitRun();
                MainSceneController.LoadMainMenu();
                return;
            }
            if (!escapeAvailable) return;

            var gm = GameManager.Instance;

            // Update confirm dialog text with extraction info
            float countdown = 30f;
            if (gm != null && gm.MapConfig != null && gm.MapConfig.extractionCountdown > 0f)
                countdown = gm.MapConfig.extractionCountdown;
            int secs = Mathf.CeilToInt(countdown);
            SetConfirmDescriptionText(
                $"Extracting takes {secs}s. During extraction you\ncannot build paths or towers - only mods.\nEnemies will keep attacking!");

            if (exitRunButtonObj != null) exitRunButtonObj.SetActive(false);
            Time.timeScale = 0f;
            escapeConfirmOverlay.SetActive(true);
        }

        private void OnEscapeConfirmed()
        {
            escapeConfirmOverlay.SetActive(false);
            Time.timeScale = 1f;

            var gm = GameManager.Instance;
            if (gm == null) return;

            float countdown = 30f;
            if (gm.MapConfig != null && gm.MapConfig.extractionCountdown > 0f)
                countdown = gm.MapConfig.extractionCountdown;

            if (!gm.StartExtraction(countdown))
                return;

            // Disable escape button during extraction
            if (escapeButton != null) escapeButton.interactable = false;
            if (escapeButtonText != null) escapeButtonText.text = "Extracting...";

            // Deselect any active card/tower
            var handUI = FindFirstObjectByType<PieceHandUI>();
            if (handUI != null)
                handUI.SwitchToModsTab(force: true);

            ShowExtractionPopup(countdown);
        }

        private void OnEscapeCancelled()
        {
            escapeConfirmOverlay.SetActive(false);
            if (exitRunButtonObj != null) exitRunButtonObj.SetActive(true);
            Time.timeScale = 1f;
        }

        private void ShowExtractionPopup(float duration)
        {
            if (extractionPopup != null) return;

            extractionPopup = new GameObject("ExtractionPopup");
            extractionPopup.transform.SetParent(canvas.transform, false);
            var rect = extractionPopup.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.3f, 0.88f);
            rect.anchorMax = new Vector2(0.7f, 0.95f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var bg = extractionPopup.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.08f, 0.2f, 0.92f);
            bg.raycastTarget = false;

            var textObj = new GameObject("TimerText");
            textObj.transform.SetParent(extractionPopup.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);
            extractionTimerText = textObj.AddComponent<TextMeshProUGUI>();
            extractionTimerText.fontSize = 22;
            extractionTimerText.fontStyle = FontStyles.Bold;
            extractionTimerText.color = new Color(1f, 0.85f, 0.2f);
            extractionTimerText.alignment = TextAlignmentOptions.Center;
            extractionTimerText.raycastTarget = false;

            int secs = Mathf.CeilToInt(duration);
            extractionTimerText.text = $"Extracting... {secs}s";
            lastExtractionSeconds = secs;
        }

        private void HideExtractionPopup()
        {
            if (extractionPopup != null)
            {
                Destroy(extractionPopup);
                extractionPopup = null;
                extractionTimerText = null;
                lastExtractionSeconds = -1;
            }
        }

        private void OnExtractionTick(float remaining, float total)
        {
            if (extractionTimerText == null) return;
            int secs = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            if (secs != lastExtractionSeconds)
            {
                lastExtractionSeconds = secs;
                extractionTimerText.text = $"Extracting... {secs}s";
            }
        }

        private void OnExtractionComplete()
        {
            HideExtractionPopup();
            GameManager.Instance?.ExitRun();
            MainSceneController.LoadMainMenu();
        }

        private void SetConfirmDescriptionText(string text)
        {
            if (escapeConfirmOverlay == null) return;
            var panel = escapeConfirmOverlay.transform.Find("Panel");
            if (panel == null) return;

            var descTransform = panel.Find("ExtractionDesc");
            TextMeshProUGUI desc;
            if (descTransform != null)
            {
                desc = descTransform.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                var descObj = new GameObject("ExtractionDesc");
                descObj.transform.SetParent(panel, false);
                var descRect = descObj.AddComponent<RectTransform>();
                descRect.anchorMin = new Vector2(0.05f, 0.15f);
                descRect.anchorMax = new Vector2(0.95f, 0.65f);
                descRect.offsetMin = Vector2.zero;
                descRect.offsetMax = Vector2.zero;
                desc = descObj.AddComponent<TextMeshProUGUI>();
                desc.fontSize = 16;
                desc.color = new Color(0.85f, 0.85f, 0.85f);
                desc.alignment = TextAlignmentOptions.Center;
                desc.raycastTarget = false;
            }
            desc.text = text;
        }

        private void OnUpgradesClicked()
        {
            if (upgradeSelectionUI == null) return;

            if (upgradeSelectionUI.IsVisible)
            {
                upgradeSelectionUI.Hide();
            }
            else
            {
                upgradeSelectionUI.ShowWithoutPause();
            }
        }

        private void OnSellClicked()
        {
            towerManager?.SellTower();
        }

        private void CreateUpgradeGlowParticles()
        {
            var psObj = new GameObject("UpgradeGlowPS");
            upgradeGlowPS = psObj.AddComponent<ParticleSystem>();
            upgradeGlowPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = upgradeGlowPS.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 0.8f;
            main.startSpeed = 2f;
            main.startSize = 0.5f;
            main.startColor = new Color(0.5f, 0.3f, 0.9f, 0.6f);
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            var emission = upgradeGlowPS.emission;
            emission.rateOverTime = 15f;

            var shape = upgradeGlowPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.5f;

            // Use default particle material (already handles transparency)
            var psRenderer = psObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            upgradeGlowPS.Stop();
            psObj.SetActive(false);
        }

        private void CreateDamageFlashOverlay()
        {
            if (canvas == null) return;
            var flashObj = new GameObject("DamageFlash");
            flashObj.transform.SetParent(canvas.transform, false);
            var rect = flashObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            damageFlashImage = flashObj.AddComponent<Image>();
            damageFlashImage.color = new Color(1f, 0f, 0f, 0f);
            damageFlashImage.raycastTarget = false;
        }

        public void FlashDamage()
        {
            damageFlashTimer = 0.35f;
        }

        public RectTransform GetStartWaveButtonRectTransform()
        {
            if (startWaveButton == null) return null;
            return startWaveButton.GetComponent<RectTransform>();
        }

        private void OnDestroy()
        {
            if (questEscapeTutActive)
                Time.timeScale = 1f;
            if (upgradeGlowPS != null)
                Destroy(upgradeGlowPS.gameObject);
            if (tutPanelObj != null)
                Destroy(tutPanelObj);
            if (tutArrowObj != null)
                Destroy(tutArrowObj);

            if (GameManager.Instance != null)
                GameManager.Instance.OnObjectivesMet -= OnObjectivesMet;

            if (QuestManager.Instance != null)
                QuestManager.Instance.OnQuestProgressChanged -= UpdateEscapeProgress;
        }
    }
}
