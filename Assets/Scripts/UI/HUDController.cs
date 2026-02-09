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
        private Text livesText;
        private Text waveText;
        private Text currencyText;
        private Dictionary<ResourceType, Text> resourceTexts = new Dictionary<ResourceType, Text>();
        private Dictionary<ResourceType, Image> resourceIcons = new Dictionary<ResourceType, Image>();
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
        private PieceHandUI pieceHandUIInstance;
        private UpgradeSelectionUI upgradeSelectionUI;
        private Button upgradesButton;
        private Text enemyCountText;
        private List<GameObject> cheatSpawnerMarkers = new List<GameObject>();
        private bool showingSpawners;

        // Continuous mode escape
        private GameObject escapeButtonObj;
        private Button escapeButton;
        private Text escapeButtonText;
        private GameObject escapeConfirmOverlay;
        private bool continuousStarted;
        private float escapeTimer;
        private const float EscapeInterval = 300f; // 5 minutes
        private bool escapeAvailable;

        // Cached values for throttling UI text updates
        private int lastBuildSeconds = -1;
        private int lastEnemyCount = -1;
        private int lastEscapeMin = -1;
        private int lastEscapeSec = -1;

        private void Awake()
        {
            CreateUI();
        }

        private void Start()
        {
            waveManager = FindObjectOfType<WaveManager>();
            towerManager = FindObjectOfType<TowerManager>();
            upgradeSelectionUI = FindObjectOfType<UpgradeSelectionUI>();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLivesChanged += UpdateLives;
                GameManager.Instance.OnCurrencyChanged += UpdateCurrency;
                GameManager.Instance.OnWaveChanged += UpdateWave;
                GameManager.Instance.OnGameOver += ShowGameOver;
                GameManager.Instance.OnBuildPhaseStarted += OnBuildPhaseStarted;
                GameManager.Instance.OnBuildPhaseEnded += OnBuildPhaseEnded;

                UpdateLives(GameManager.Instance.Lives);
                UpdateCurrency(GameManager.Instance.Currency);
                UpdateWave(GameManager.Instance.Wave);

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
        }

        private void CreateUI()
        {
            // Create EventSystem if it doesn't exist (required for UI interaction)
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            // Create Canvas
            GameObject canvasObj = new GameObject("HUD_Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Top bar background
            CreatePanel(canvasObj.transform, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -30), new Vector2(400, 60), new Color(0, 0, 0, 0.7f));

            // Lives text
            livesText = CreateText(canvasObj.transform, "LivesText", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(80, -30), "Lives: 10");

            // Wave text
            waveText = CreateText(canvasObj.transform, "WaveText", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -30), "Wave: 0");

            // Resource panel (top-right, vertical layout with gold on top)
            CreateResourcePanel(canvasObj.transform);

            // Start Wave button (raised to not overlap piece hand panel)
            startWaveButton = CreateButton(canvasObj.transform, "StartWaveButton", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-90, 200), new Vector2(160, 50), "Start Wave", OnStartWaveClicked);

            // Exit Run button (next to Start Wave, hidden until wave 1 completes)
            var exitBtn = CreateButton(canvasObj.transform, "ExitRunButton", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(90, 200), new Vector2(160, 50), "Exit Run", OnExitRunClicked);
            exitRunButtonObj = exitBtn.gameObject;
            // Style it red to distinguish from Start Wave
            exitRunButtonObj.GetComponent<Image>().color = new Color(0.6f, 0.2f, 0.2f);
            exitRunButtonObj.SetActive(false);

            // Escape button (continuous mode, beside Start Wave)
            escapeButton = CreateButton(canvasObj.transform, "EscapeButton", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(90, 200), new Vector2(160, 50), "Escape", OnEscapeClicked);
            escapeButtonObj = escapeButton.gameObject;
            escapeButtonObj.GetComponent<Image>().color = new Color(0.6f, 0.5f, 0.1f);
            escapeButtonText = escapeButtonObj.GetComponentInChildren<Text>();
            escapeButton.interactable = false;
            escapeButtonObj.SetActive(false);

            // Escape confirmation overlay
            CreateEscapeConfirmOverlay(canvasObj.transform);

            // Build phase countdown timer (above Start Wave / Exit Run buttons)
            buildTimerObj = new GameObject("BuildTimer");
            buildTimerObj.transform.SetParent(canvasObj.transform);

            var timerRect = buildTimerObj.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 0);
            timerRect.anchorMax = new Vector2(0.5f, 0);
            timerRect.anchoredPosition = new Vector2(0, 260);
            timerRect.sizeDelta = new Vector2(300, 40);

            buildTimerText = buildTimerObj.AddComponent<Text>();
            buildTimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buildTimerText.fontSize = 22;
            buildTimerText.color = new Color(1f, 0.9f, 0.4f);
            buildTimerText.alignment = TextAnchor.MiddleCenter;
            buildTimerObj.SetActive(false);

            // Tower selection panel (hidden by default)
            towerPanel = CreateTowerPanel(canvasObj.transform);
            towerPanel.SetActive(false);

            // Tower info panel (hidden by default)
            towerInfoPanel = CreateTowerInfoPanel(canvasObj.transform);
            towerInfoPanel.SetActive(false);

            // Upgrades button (always visible, both modes)
            upgradesButton = CreateButton(canvasObj.transform, "UpgradesButton", new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(100, 200), new Vector2(140, 50), "Upgrades", OnUpgradesClicked);
            upgradesButton.GetComponent<Image>().color = new Color(0.3f, 0.2f, 0.6f);

            // Enemy count text (for continuous mode)
            enemyCountText = CreateText(canvasObj.transform, "EnemyCountText", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(160, -30), "");

            // Create upgrade selection UI
            var upgradeSelectionObj = new GameObject("UpgradeSelectionUI");
            upgradeSelectionObj.transform.SetParent(canvasObj.transform);
            upgradeSelectionObj.AddComponent<UpgradeSelectionUI>();

            // Create picked cards UI
            var pickedCardsObj = new GameObject("PickedCardsUI");
            pickedCardsObj.transform.SetParent(canvasObj.transform);
            pickedCardsObj.AddComponent<PickedCardsUI>();

            // Create piece hand UI
            var pieceHandObj = new GameObject("PieceHandUI");
            pieceHandObj.transform.SetParent(canvasObj.transform);
            var pieceHandRect = pieceHandObj.AddComponent<RectTransform>();
            pieceHandRect.anchorMin = Vector2.zero;
            pieceHandRect.anchorMax = Vector2.one;
            pieceHandRect.offsetMin = Vector2.zero;
            pieceHandRect.offsetMax = Vector2.zero;
            pieceHandUIInstance = pieceHandObj.AddComponent<PieceHandUI>();
            pieceHandUIInstance.Initialize(canvas);

            // Cheat panel
            CreateCheatPanel(canvasObj.transform);
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
            buttonObj.transform.SetParent(parent);

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
            textObj.transform.SetParent(buttonObj.transform);
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
            var panel = CreatePanel(parent, "TowerPanel", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 200), new Vector2(800, 80), new Color(0, 0, 0, 0.8f));

            // Will be populated with tower buttons dynamically
            return panel;
        }

        private GameObject CreateTowerInfoPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "TowerInfoPanel", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 200), new Vector2(200, 100), new Color(0, 0, 0, 0.8f));

            CreateButton(panel.transform, "SellButton", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 30), new Vector2(120, 40), "Sell", OnSellClicked);

            return panel;
        }

        private void UpdateLives(int lives)
        {
            if (livesText != null)
                livesText.text = $"Lives: {lives}";
        }

        private void UpdateCurrency(int currency)
        {
            if (currencyText != null)
                currencyText.text = $"Gold: {currency}g";
        }

        private void UpdateWave(int wave)
        {
            if (waveText != null)
                waveText.text = $"Wave: {wave}";
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

            // Show enemy count in continuous mode
            if (enemyCountText != null && waveManager != null && waveManager.IsContinuousMode)
            {
                int count = waveManager.EnemyCount;
                if (count != lastEnemyCount)
                {
                    lastEnemyCount = count;
                    enemyCountText.text = $"Enemies: {count}";
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
                text.text = $"{name}: {pm.GetBanked(resType)}(+{pm.GetRunGathered(resType)})";
            }
        }

        private void CreateCheatPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "CheatPanel", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-80, 0), new Vector2(140, 90), new Color(0.3f, 0f, 0f, 0.7f));

            CreateButton(panel.transform, "CheatGold", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 18), new Vector2(120, 32), "+1000 Gold", OnCheatGold);

            CreateButton(panel.transform, "CheatResources", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -18), new Vector2(120, 32), "+100 Res", OnCheatResources);

            CreateButton(panel.transform, "CheatShowCamps", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -54), new Vector2(120, 32), "Show Camps", OnCheatShowCamps);

            // Expand panel to fit third button
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(140, 130);
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

        private void CreateEscapeConfirmOverlay(Transform parent)
        {
            escapeConfirmOverlay = new GameObject("EscapeConfirmOverlay");
            escapeConfirmOverlay.transform.SetParent(parent);

            var rect = escapeConfirmOverlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = escapeConfirmOverlay.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.75f);

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(escapeConfirmOverlay.transform);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(400, 180);
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.12f, 0.12f, 0.18f);

            // Question text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(panel.transform);
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
            escapeConfirmOverlay.transform.SetAsLastSibling();
            Time.timeScale = 0f;
        }

        private void OnEscapeConfirmed()
        {
            Time.timeScale = 1f;
            escapeConfirmOverlay.SetActive(false);
            GameManager.Instance?.ExitRun();
        }

        private void OnEscapeCancelled()
        {
            Time.timeScale = 1f;
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
    }
}