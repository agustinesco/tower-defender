using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Grid;
using TowerDefense.Entities;
using TowerDefense.UI;

namespace TowerDefense.Core
{
    public class TutorialManager : MonoBehaviour
    {
        private enum TutorialStep
        {
            Welcome,
            Mining,
            SpawnPoints,
            PlaceTower,
            StartWave,
            Upgrades,
            Escape,
            Done
        }

        private const string PrefKey = "tutorial_complete";

        private static readonly string[] StepMessages = new string[]
        {
            "Select a path card on the left, then hold on a valid hex to build it. Tap the card again to rotate before placing. You can also place over existing tiles to replace them!",
            "Ore deposits are scattered nearby (colored icons). Place a path over one to auto-build a mine and gather resources each wave!",
            "Enemies spawn at open path edges (goblin icons). More paths = more spawn points!",
            "Switch to the Towers tab and tap a tower card, then tap near a path to place it.",
            "Towers attack enemies automatically. Press 'Start Wave' when you're ready!",
            "You survived! The shop lets you spend gathered resources on permanent upgrades.",
            "Resources you gather are only kept if you Exit Run or Escape. Dying loses everything!"
        };

        private Canvas canvas;
        private GameObject panel;
        private RectTransform panelRect;
        private Text messageText;
        private Button okButton;
        private TutorialStep currentStep;

        private GameObject arrowObj;
        private RectTransform arrowRect;
        private Text arrowText;

        private PieceDragHandler pieceDragHandler;
        private TowerManager towerManager;
        private WaveManager waveManager;
        private CameraController cameraController;
        private Vector3 savedCameraPos;

        private void Start()
        {
            if (PlayerPrefs.GetInt(PrefKey, 0) == 1)
            {
                Debug.Log("TutorialManager: tutorial already completed, skipping.");
                Destroy(gameObject);
                return;
            }

            Debug.Log("TutorialManager: starting first-run tutorial.");

            pieceDragHandler = FindFirstObjectByType<PieceDragHandler>();
            towerManager = FindFirstObjectByType<TowerManager>();
            waveManager = FindFirstObjectByType<WaveManager>();
            cameraController = FindFirstObjectByType<CameraController>();

            CreateUI();
            Subscribe();

            panel.SetActive(false);
            arrowObj.SetActive(false);
            Invoke(nameof(ShowWelcome), 0.3f);
        }

        private void ShowWelcome()
        {
            ShowStep(TutorialStep.Welcome);
        }

        private void Subscribe()
        {
            if (pieceDragHandler != null)
                pieceDragHandler.OnPiecePlaced += OnPiecePlaced;
            if (towerManager != null)
                towerManager.OnTowerPlaced += OnTowerPlaced;
            if (waveManager != null)
                waveManager.OnWaveComplete += OnWaveComplete;
        }

        private void OnDestroy()
        {
            if (pieceDragHandler != null)
                pieceDragHandler.OnPiecePlaced -= OnPiecePlaced;
            if (towerManager != null)
                towerManager.OnTowerPlaced -= OnTowerPlaced;
            if (waveManager != null)
                waveManager.OnWaveComplete -= OnWaveComplete;

            if (canvas != null)
                Destroy(canvas.gameObject);
        }

        private void CreateUI()
        {
            // Canvas
            var canvasObj = new GameObject("TutorialCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // SafeArea child
            var safeObj = new GameObject("SafeArea");
            safeObj.transform.SetParent(canvasObj.transform, false);
            var safeRect = safeObj.AddComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;
            safeObj.AddComponent<SafeArea>();

            // Panel (centered, 700x160)
            panel = new GameObject("TutorialPanel");
            panel.transform.SetParent(safeObj.transform, false);
            panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700, 160);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

            // Message text
            var textObj = new GameObject("Message");
            textObj.transform.SetParent(panel.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0.35f);
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20, 0);
            textRect.offsetMax = new Vector2(-20, -10);

            messageText = textObj.AddComponent<Text>();
            messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            messageText.fontSize = 24;
            messageText.color = Color.white;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            messageText.verticalOverflow = VerticalWrapMode.Overflow;

            // OK button
            var btnObj = new GameObject("OKButton");
            btnObj.transform.SetParent(panel.transform, false);
            var btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.sizeDelta = new Vector2(100, 36);
            btnRect.anchoredPosition = new Vector2(0, 22);

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.55f, 0.9f, 1f);

            okButton = btnObj.AddComponent<Button>();
            okButton.targetGraphic = btnImg;
            okButton.onClick.AddListener(OnOKPressed);

            var btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            var btnText = btnTextObj.AddComponent<Text>();
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize = 22;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.text = "OK";

            // Arrow indicator (child of SafeArea so it can be positioned anywhere)
            arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(safeObj.transform, false);
            arrowRect = arrowObj.AddComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(80, 80);

            arrowText = arrowObj.AddComponent<Text>();
            arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrowText.fontSize = 64;
            arrowText.color = new Color(0.3f, 0.7f, 1f);
            arrowText.alignment = TextAnchor.MiddleCenter;

            var arrowOutline = arrowObj.AddComponent<Outline>();
            arrowOutline.effectColor = Color.black;
            arrowOutline.effectDistance = new Vector2(2, -2);
        }

        private void ShowStep(TutorialStep step)
        {
            currentStep = step;

            if (step == TutorialStep.Done)
            {
                CompleteTutorial();
                return;
            }

            // Pan camera to deposit for Mining step
            if (step == TutorialStep.Mining)
                PanToNearestDeposit();

            messageText.text = StepMessages[(int)step];
            panel.SetActive(true);
            UpdateArrow(step);
        }

        private void PanToNearestDeposit()
        {
            if (GameManager.Instance == null || cameraController == null)
                return;

            var nearest = GameManager.Instance.GetNearestOreDeposit();
            if (!nearest.HasValue)
                return;

            savedCameraPos = cameraController.transform.position;
            Vector3 depositWorld = HexGrid.HexToWorld(nearest.Value);
            cameraController.PanToPosition(depositWorld);
        }

        private void PanBackFromDeposit()
        {
            if (cameraController == null)
                return;

            cameraController.PanToPosition(savedCameraPos);
        }

        private void UpdateArrow(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Welcome:
                    // Arrow pointing left at path cards
                    arrowObj.SetActive(true);
                    arrowRect.anchorMin = new Vector2(0.14f, 0.5f);
                    arrowRect.anchorMax = new Vector2(0.14f, 0.5f);
                    arrowRect.anchoredPosition = Vector2.zero;
                    arrowText.text = "\u25C0";
                    break;

                case TutorialStep.PlaceTower:
                    // Arrow pointing left at Towers tab
                    arrowObj.SetActive(true);
                    arrowRect.anchorMin = new Vector2(0.14f, 0.38f);
                    arrowRect.anchorMax = new Vector2(0.14f, 0.38f);
                    arrowRect.anchoredPosition = Vector2.zero;
                    arrowText.text = "\u25C0";
                    break;

                case TutorialStep.StartWave:
                    // Arrow pointing right at Start Wave button
                    arrowObj.SetActive(true);
                    arrowRect.anchorMin = new Vector2(0.84f, 0.12f);
                    arrowRect.anchorMax = new Vector2(0.84f, 0.12f);
                    arrowRect.anchoredPosition = Vector2.zero;
                    arrowText.text = "\u25B6";
                    break;

                default:
                    arrowObj.SetActive(false);
                    break;
            }
        }

        private void HideAll()
        {
            panel.SetActive(false);
            arrowObj.SetActive(false);
        }

        private void OnOKPressed()
        {
            // Pan camera back after Mining step
            if (currentStep == TutorialStep.Mining)
                PanBackFromDeposit();

            HideAll();

            switch (currentStep)
            {
                case TutorialStep.Welcome:
                    ShowStep(TutorialStep.Mining);
                    break;
                case TutorialStep.Mining:
                    // Wait for OnPiecePlaced to advance
                    break;
                case TutorialStep.SpawnPoints:
                    ShowStep(TutorialStep.PlaceTower);
                    break;
                case TutorialStep.PlaceTower:
                    // Wait for OnTowerPlaced to advance
                    break;
                case TutorialStep.StartWave:
                    // Wait for OnWaveComplete to advance
                    break;
                case TutorialStep.Upgrades:
                    ShowStep(TutorialStep.Escape);
                    break;
                case TutorialStep.Escape:
                    ShowStep(TutorialStep.Done);
                    break;
            }
        }

        private void OnPiecePlaced(int handIndex, PlacementRotation rotation, HexCoord coord)
        {
            if (currentStep == TutorialStep.Mining)
                ShowStep(TutorialStep.SpawnPoints);
        }

        private void OnTowerPlaced(Tower tower)
        {
            if (currentStep == TutorialStep.PlaceTower)
                ShowStep(TutorialStep.StartWave);
        }

        private void OnWaveComplete()
        {
            if (currentStep == TutorialStep.StartWave)
                ShowStep(TutorialStep.Upgrades);
        }

        private void CompleteTutorial()
        {
            PlayerPrefs.SetInt(PrefKey, 1);
            PlayerPrefs.Save();
            HideAll();
            Destroy(gameObject);
        }
    }
}
