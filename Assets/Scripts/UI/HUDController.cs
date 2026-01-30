using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Grid;
using TowerDefense.Entities;


namespace TowerDefense.UI
{
    public class HUDController : MonoBehaviour
    {
        private Text livesText;
        private Text waveText;
        private Text currencyText;
        private Button startWaveButton;
        private GameObject towerPanel;
        private GameObject towerInfoPanel;
        private List<GameObject> towerButtons = new List<GameObject>();

        private Canvas canvas;
        private WaveManager waveManager;
        private TowerManager towerManager;

        private void Awake()
        {
            CreateUI();
        }

        private void Start()
        {
            waveManager = FindObjectOfType<WaveManager>();
            towerManager = FindObjectOfType<TowerManager>();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLivesChanged += UpdateLives;
                GameManager.Instance.OnCurrencyChanged += UpdateCurrency;
                GameManager.Instance.OnWaveChanged += UpdateWave;
                GameManager.Instance.OnGameOver += ShowGameOver;

                UpdateLives(GameManager.Instance.Lives);
                UpdateCurrency(GameManager.Instance.Currency);
                UpdateWave(GameManager.Instance.Wave);
            }

            if (towerManager != null)
            {
                towerManager.OnSlotSelected += ShowTowerPanel;
                towerManager.OnTowerSelected += ShowTowerInfo;
                towerManager.OnSelectionCleared += HidePanels;
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
            canvasObj.AddComponent<CanvasScaler>();
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

            // Currency text
            currencyText = CreateText(canvasObj.transform, "CurrencyText", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-80, -30), "Gold: 200");

            // Start Wave button
            startWaveButton = CreateButton(canvasObj.transform, "StartWaveButton", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 100), new Vector2(160, 50), "Start Wave", OnStartWaveClicked);

            // Tower selection panel (hidden by default)
            towerPanel = CreateTowerPanel(canvasObj.transform);
            towerPanel.SetActive(false);

            // Tower info panel (hidden by default)
            towerInfoPanel = CreateTowerInfoPanel(canvasObj.transform);
            towerInfoPanel.SetActive(false);
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
                new Vector2(0, 200), new Vector2(350, 80), new Color(0, 0, 0, 0.8f));

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
                currencyText.text = $"Gold: {currency}";
        }

        private void UpdateWave(int wave)
        {
            if (waveText != null)
                waveText.text = $"Wave: {wave}";
        }

        private void ShowTowerPanel(TowerSlot slot)
        {
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
            // Simple game over display
            CreateText(canvas.transform, "GameOverText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, "GAME OVER");
        }

        private void OnStartWaveClicked()
        {
            Debug.Log($"HUD: Start Wave button clicked. WaveManager: {(waveManager != null ? "Found" : "NULL")}");
            if (waveManager != null)
            {
                waveManager.StartWave();
            }
            else
            {
                Debug.LogError("HUD: WaveManager is null! Cannot start wave.");
            }
        }

        private void OnSellClicked()
        {
            towerManager?.SellTower();
        }
    }
}