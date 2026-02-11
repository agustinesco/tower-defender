using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Grid;
using TowerDefense.Entities;
using TowerDefense.Data;


namespace TowerDefense.UI
{
    public class HUDController : MonoBehaviour
    {
        private Image livesBarFill;
        private Text livesBarText;
        private int lastLives = -1;
        private Text currencyText;
        private Dictionary<ResourceType, Text> resourceTexts = new Dictionary<ResourceType, Text>();
        private Dictionary<ResourceType, Image> resourceIcons = new Dictionary<ResourceType, Image>();
        private Dictionary<ResourceType, GameObject> resourceRows = new Dictionary<ResourceType, GameObject>();
        private Image goldIcons;
        private Text buildTimerText;
        private Button startWaveButton;
        private GameObject exitRunButtonObj;
        private GameObject buildTimerObj;
        private GameObject towerPanel;
        private GameObject towerInfoPanel;
        private List<GameObject> towerButtons = new List<GameObject>();

        private Canvas canvas;
        private WaveManager waveManager;
        private TowerManager towerManager;
        private UpgradeSelectionUI upgradeSelectionUI;
        private Button upgradesButton;
        private List<GameObject> cheatSpawnerMarkers = new List<GameObject>();
        private bool showingSpawners;
        private GameObject cheatPanelObj;

        // Continuous mode escape
        private GameObject escapeButtonObj;
        private Button escapeButton;
        private Text escapeButtonText;
        private GameObject escapeConfirmOverlay;
        private bool continuousStarted;
        private float escapeTimer;
        private const float EscapeInterval = 300f; // 5 minutes
        private bool escapeAvailable;

        private CanvasScaler canvasScaler;

        // Upgrade button glow
        private Image upgradesButtonImage;
        private bool canAffordUpgrade;
        private float upgradeCheckTimer;
        private static readonly Color NormalUpgradeColor = new Color(0.3f, 0.2f, 0.6f);

        // Particle glow for upgrade button
        private ParticleSystem upgradeGlowPS;
        private Camera cachedCamera;

        // Overlay canvas for escape confirm (renders above PieceHandUI_Canvas)
        private GameObject escapeOverlayCanvasObj;

        // Cached values for throttling UI text updates
        private int lastBuildSeconds = -1;
        private int lastEscapeMin = -1;
        private int lastEscapeSec = -1;

        private void Awake()
        {
            CreateUI();
        }

        private void Start()
        {
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
                if (goldIcons != null)
                {
                    var goldSprite = GameManager.Instance.GoldSprite;
                    if (goldSprite != null)
                        goldIcons.sprite = goldSprite;
                    goldIcons.color = new Color(1f, 0.85f, 0.2f);
                }
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

            cachedCamera = Camera.main;
            CreateUpgradeGlowParticles();
        }

        private void CreateUI()
        {
            // Create EventSystem if it doesn't exist (required for UI interaction)
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            // Create Canvas
            GameObject canvasObj = new GameObject("HUD_Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvasScaler = canvasObj.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.matchWidthOrHeight = 1f;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObj.AddComponent<GraphicRaycaster>();

            // Use device safe area for proper anchoring on all devices
            var safeAreaObj = new GameObject("SafeArea");
            safeAreaObj.transform.SetParent(canvasObj.transform);
            var safeRect = safeAreaObj.AddComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;
            safeAreaObj.AddComponent<SafeArea>();
            Transform uiRoot = safeAreaObj.transform;

            // Sub-canvas for static UI (rarely changes — avoids Canvas rebuild from dynamic text)
            Transform staticRoot = CreateSubCanvas(uiRoot, "StaticUI");
            // Sub-canvas for dynamic UI (text that changes frequently)
            Transform dynamicRoot = CreateSubCanvas(uiRoot, "DynamicUI");

            // Lives bar (dynamic, top-left)
            CreateLivesBar(dynamicRoot);

            // Resource panel (dynamic — text updates)
            CreateResourcePanel(dynamicRoot);

            // Start Wave button (static)
            startWaveButton = CreateButton(staticRoot, "StartWaveButton", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-85, 170), new Vector2(150, 50), "Start Wave", OnStartWaveClicked);

            // Exit Run button (static)
            var exitBtn = CreateButton(staticRoot, "ExitRunButton", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-85, 115), new Vector2(150, 50), "Exit Run", OnExitRunClicked);
            exitRunButtonObj = exitBtn.gameObject;
            exitRunButtonObj.GetComponent<Image>().color = new Color(0.6f, 0.2f, 0.2f);
            exitRunButtonObj.SetActive(false);

            // Escape button (dynamic — timer text changes)
            escapeButton = CreateButton(dynamicRoot, "EscapeButton", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-85, 115), new Vector2(150, 50), "Escape", OnEscapeClicked);
            escapeButtonObj = escapeButton.gameObject;
            escapeButtonObj.GetComponent<Image>().color = new Color(0.6f, 0.5f, 0.1f);
            escapeButtonText = escapeButtonObj.GetComponentInChildren<Text>();
            escapeButton.interactable = false;
            escapeButtonObj.SetActive(false);

            // Escape confirmation overlay (its own canvas, above PieceHandUI)
            CreateEscapeConfirmOverlay();

            // Build phase countdown timer (dynamic)
            buildTimerObj = new GameObject("BuildTimer");
            buildTimerObj.transform.SetParent(dynamicRoot);

            var timerRect = buildTimerObj.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(1, 0);
            timerRect.anchorMax = new Vector2(1, 0);
            timerRect.anchoredPosition = new Vector2(-105, 225);
            timerRect.sizeDelta = new Vector2(210, 40);

            buildTimerText = buildTimerObj.AddComponent<Text>();
            buildTimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buildTimerText.fontSize = 22;
            buildTimerText.color = new Color(1f, 0.9f, 0.4f);
            buildTimerText.alignment = TextAnchor.MiddleCenter;
            buildTimerObj.SetActive(false);

            // Tower selection panel (static — hidden by default)
            towerPanel = CreateTowerPanel(staticRoot);
            towerPanel.SetActive(false);

            // Tower info panel (static — hidden by default)
            towerInfoPanel = CreateTowerInfoPanel(staticRoot);
            towerInfoPanel.SetActive(false);

            // Upgrades button (static)
            upgradesButton = CreateButton(staticRoot, "UpgradesButton", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-85, 60), new Vector2(150, 50), "Upgrades", OnUpgradesClicked);
            upgradesButtonImage = upgradesButton.GetComponent<Image>();
            upgradesButtonImage.color = NormalUpgradeColor;

            // Create upgrade selection UI (under uiRoot — has its own canvas)
            var upgradeSelectionObj = new GameObject("UpgradeSelectionUI");
            upgradeSelectionObj.transform.SetParent(uiRoot);
            upgradeSelectionObj.AddComponent<UpgradeSelectionUI>();

            // Create picked cards UI
            var pickedCardsObj = new GameObject("PickedCardsUI");
            pickedCardsObj.transform.SetParent(uiRoot);
            pickedCardsObj.AddComponent<PickedCardsUI>();

            // Cheat panel (static)
            CreateCheatPanel(staticRoot);
        }

        private Transform CreateSubCanvas(Transform parent, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            obj.AddComponent<Canvas>(); // Sub-canvas isolates rebuild
            obj.AddComponent<GraphicRaycaster>(); // Required for button clicks
            return obj.transform;
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = panel.AddComponent<Image>();
            image.color = color;

            return panel;
        }

        private Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, string content)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent);

            var rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(150, 40);

            var text = textObj.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            return text;
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

            var text = textObj.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            return button;
        }

        private GameObject CreateTowerPanel(Transform parent)
        {
            // Stretch horizontally so it scales with screen width
            var panel = new GameObject("TowerPanel");
            panel.transform.SetParent(parent);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.15f, 0);
            panelRect.anchorMax = new Vector2(0.85f, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, 230);
            panelRect.sizeDelta = new Vector2(0, 80);
            panel.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);

            // Will be populated with tower buttons dynamically
            return panel;
        }

        private GameObject CreateTowerInfoPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "TowerInfoPanel", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 230), new Vector2(200, 100), new Color(0, 0, 0, 0.8f));

            CreateButton(panel.transform, "SellButton", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 30), new Vector2(120, 40), "Sell", OnSellClicked);

            return panel;
        }

        private void CreateLivesBar(Transform parent)
        {
            // Container anchored at top-left
            var container = new GameObject("LivesBar");
            container.transform.SetParent(parent);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 1);
            containerRect.anchorMax = new Vector2(0, 1);
            containerRect.pivot = new Vector2(0, 1);
            containerRect.anchoredPosition = new Vector2(10, -10);
            containerRect.sizeDelta = new Vector2(200, 28);

            // Background (dark bar)
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(container.transform);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            // Fill bar (uses Filled image type)
            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(container.transform);
            var fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);
            livesBarFill = fillObj.AddComponent<Image>();
            livesBarFill.type = Image.Type.Filled;
            livesBarFill.fillMethod = Image.FillMethod.Horizontal;
            livesBarFill.fillAmount = 1f;
            livesBarFill.color = new Color(0.2f, 0.75f, 0.2f);

            // Text overlay (centered on bar)
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(container.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            livesBarText = textObj.AddComponent<Text>();
            livesBarText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            livesBarText.fontSize = 16;
            livesBarText.color = Color.white;
            livesBarText.alignment = TextAnchor.MiddleCenter;
            livesBarText.text = "";
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
                currencyText.text = $"Gold: {currency}g";
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

                var text = textObj.AddComponent<Text>();
                text.text = $"{towerData.towerName}\n{towerData.cost}g";
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 14;
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleCenter;

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
                escapeTimer = 0f;
                escapeAvailable = false;
                if (startWaveButton != null)
                    startWaveButton.gameObject.SetActive(false);
                if (exitRunButtonObj != null)
                    exitRunButtonObj.SetActive(false);
                if (escapeButtonObj != null)
                {
                    escapeButtonObj.SetActive(true);
                    escapeButton.interactable = false;
                }
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
            var text = button.GetComponentInChildren<Text>();
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

            // Continuous escape timer
            if (continuousStarted && escapeButtonObj != null && escapeButtonObj.activeSelf)
            {
                escapeTimer += Time.deltaTime;
                float remaining = EscapeInterval - (escapeTimer % EscapeInterval);

                if (!escapeAvailable && escapeTimer >= EscapeInterval)
                {
                    escapeAvailable = true;
                    escapeButton.interactable = true;
                    escapeButtonObj.GetComponent<Image>().color = new Color(0.8f, 0.65f, 0.1f);
                }

                if (escapeAvailable)
                {
                    if (escapeButtonText != null)
                        escapeButtonText.text = "Escape!";
                }
                else
                {
                    int min = Mathf.FloorToInt(remaining / 60f);
                    int sec = Mathf.CeilToInt(remaining % 60f);
                    if (sec == 60) { min++; sec = 0; }
                    if (min != lastEscapeMin || sec != lastEscapeSec)
                    {
                        lastEscapeMin = min;
                        lastEscapeSec = sec;
                        if (escapeButtonText != null)
                            escapeButtonText.text = $"Escape {min}:{sec:D2}";
                    }
                }
            }
        }

        private void CreateResourcePanel(Transform parent)
        {
            int totalRows = 5; // gold + 4 resources
            float rowHeight = 26f;
            float panelHeight = totalRows * rowHeight + 16f;

            var panel = CreatePanel(parent, "ResourcePanel", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-10, -10), new Vector2(160, panelHeight), new Color(0, 0, 0, 0.6f));
            panel.GetComponent<RectTransform>().pivot = new Vector2(1, 1);

            float startY = (totalRows - 1) * rowHeight / 2f;

            // Gold row (first)
            CreateResourceRow(panel.transform, "Gold", startY, rowHeight, out currencyText, out var goldIcon);
            goldIcons = goldIcon;
            currencyText.text = "Gold: 200";

            // Resource rows
            var resourceTypes = new[] {
                (ResourceType.IronOre, "Iron"),
                (ResourceType.Gems, "Gems"),
                (ResourceType.Florpus, "Florpus"),
                (ResourceType.Adamantite, "Adam")
            };

            for (int i = 0; i < resourceTypes.Length; i++)
            {
                var (resType, label) = resourceTypes[i];
                float yPos = startY - (i + 1) * rowHeight;

                CreateResourceRow(panel.transform, label, yPos, rowHeight, out var text, out var icon);
                resourceTexts[resType] = text;
                resourceIcons[resType] = icon;
                resourceRows[resType] = text.transform.parent.gameObject;
                text.text = $"{label}: 0(+0)";
            }
        }

        private void CreateResourceRow(Transform parent, string label, float yPos, float rowHeight, out Text text, out Image icon)
        {
            var row = new GameObject($"Row_{label}");
            row.transform.SetParent(parent);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0, 0.5f);
            rowRect.anchorMax = new Vector2(1, 0.5f);
            rowRect.anchoredPosition = new Vector2(0, yPos);
            rowRect.sizeDelta = new Vector2(0, rowHeight);

            // Sprite icon
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(row.transform);
            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(18, 0);
            iconRect.sizeDelta = new Vector2(22, 22);

            icon = iconObj.AddComponent<Image>();
            icon.color = Color.white;

            // Text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(row.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(34, 0);
            textRect.offsetMax = new Vector2(-4, 0);

            text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
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

        private void CreateCheatPanel(Transform parent)
        {
            // Toggle button (always visible)
            var toggleBtn = CreateButton(parent, "CheatToggle", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-20, 0), new Vector2(30, 30), "?", OnCheatToggle);
            toggleBtn.GetComponent<Image>().color = new Color(0.3f, 0f, 0f, 0.5f);

            // Cheat panel (starts hidden)
            cheatPanelObj = CreatePanel(parent, "CheatPanel", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-80, 0), new Vector2(140, 90), new Color(0.3f, 0f, 0f, 0.7f));

            CreateButton(cheatPanelObj.transform, "CheatGold", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 18), new Vector2(120, 32), "+1000 Gold", OnCheatGold);

            CreateButton(cheatPanelObj.transform, "CheatResources", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -18), new Vector2(120, 32), "+100 Res", OnCheatResources);

            CreateButton(cheatPanelObj.transform, "CheatShowCamps", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -54), new Vector2(120, 32), "Show Camps", OnCheatShowCamps);

            CreateButton(cheatPanelObj.transform, "CheatUnlockTowers", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -90), new Vector2(120, 32), "All Towers", OnCheatUnlockTowers);

            CreateButton(cheatPanelObj.transform, "CheatReset", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -126), new Vector2(120, 32), "Reset All", OnCheatResetProgress);

            CreateButton(cheatPanelObj.transform, "CheatForceEscape", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -162), new Vector2(120, 32), "Force Escape", OnCheatForceEscape);

            var panelRect = cheatPanelObj.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(140, 236);

            cheatPanelObj.SetActive(false);
        }

        private void OnCheatToggle()
        {
            if (cheatPanelObj != null)
                cheatPanelObj.SetActive(!cheatPanelObj.activeSelf);
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) PlayerPrefs.Save();
        }

        private void OnApplicationQuit()
        {
            PlayerPrefs.Save();
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
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            MainSceneController.LoadMainMenu();
        }

        private void OnCheatForceEscape()
        {
            if (!continuousStarted) return;
            escapeAvailable = true;
            if (escapeButton != null) escapeButton.interactable = true;
            if (escapeButtonObj != null)
                escapeButtonObj.GetComponent<Image>().color = new Color(0.8f, 0.65f, 0.1f);
            if (escapeButtonText != null)
                escapeButtonText.text = "Escape!";
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

        private void CreateEscapeConfirmOverlay()
        {
            // Create a separate high-sortingOrder canvas so overlay renders above PieceHandUI_Canvas
            escapeOverlayCanvasObj = new GameObject("EscapeOverlayCanvas");
            var overlayCanvas = escapeOverlayCanvasObj.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 10;
            var overlayScaler = escapeOverlayCanvasObj.AddComponent<CanvasScaler>();
            overlayScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            overlayScaler.matchWidthOrHeight = 1f;
            overlayScaler.referenceResolution = new Vector2(1920f, 1080f);
            escapeOverlayCanvasObj.AddComponent<GraphicRaycaster>();

            // Safe area wrapper
            var escapeSafeObj = new GameObject("SafeArea");
            escapeSafeObj.transform.SetParent(escapeOverlayCanvasObj.transform, false);
            var escapeSafeRect = escapeSafeObj.AddComponent<RectTransform>();
            escapeSafeRect.anchorMin = Vector2.zero;
            escapeSafeRect.anchorMax = Vector2.one;
            escapeSafeRect.offsetMin = Vector2.zero;
            escapeSafeRect.offsetMax = Vector2.zero;
            escapeSafeObj.AddComponent<SafeArea>();

            escapeConfirmOverlay = new GameObject("EscapeConfirmOverlay");
            escapeConfirmOverlay.transform.SetParent(escapeSafeObj.transform, false);

            var rect = escapeConfirmOverlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = escapeConfirmOverlay.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.75f);

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(escapeConfirmOverlay.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(400, 180);
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.12f, 0.12f, 0.18f);

            // Question text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(panel.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0.5f);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, -10);
            var text = textObj.AddComponent<Text>();
            text.text = "Escape with your resources?";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            // Yes button
            CreateButton(panel.transform, "YesBtn", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-80, 40), new Vector2(120, 45), "Yes", OnEscapeConfirmed);

            // No button
            var noBtn = CreateButton(panel.transform, "NoBtn", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(80, 40), new Vector2(120, 45), "No", OnEscapeCancelled);
            noBtn.GetComponent<Image>().color = new Color(0.5f, 0.2f, 0.2f);

            escapeConfirmOverlay.SetActive(false);
        }

        private void OnEscapeClicked()
        {
            if (!escapeAvailable) return;
            escapeConfirmOverlay.SetActive(true);
        }

        private void OnEscapeConfirmed()
        {
            escapeConfirmOverlay.SetActive(false);
            GameManager.Instance?.ExitRun();
        }

        private void OnEscapeCancelled()
        {
            escapeConfirmOverlay.SetActive(false);
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

        public RectTransform GetStartWaveButtonRectTransform()
        {
            if (startWaveButton == null) return null;
            return startWaveButton.GetComponent<RectTransform>();
        }

        private void OnDestroy()
        {
            if (upgradeGlowPS != null)
                Destroy(upgradeGlowPS.gameObject);
            if (escapeOverlayCanvasObj != null)
                Destroy(escapeOverlayCanvasObj);
        }
    }
}