using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Grid;
using TowerDefense.Entities;
using TowerDefense.UI;

namespace TowerDefense.Core
{
    public class TutorialManager : MonoBehaviour
    {
        public enum TutorialStep
        {
            SelectPathCard,
            BuildOnOre,
            MineExplanation,
            SpawnExplanation,
            PathEditInfo,
            SwitchToTowers,
            SelectTower,
            PlaceTower,
            StartWave,
            Complete,
            Done
        }

        public static TutorialManager Instance { get; private set; }

        private const string PrefPrefix = "tut_";
        private const int StepCount = 10; // Everything before Done

        private static readonly string[] StepMessages = new string[]
        {
            "Select a path card",
            "Place a path on the ore deposit to automatically build a mine!",
            "Mine placed! It will gather resources over time while you play. At the end of each run, collected resources can be spent on permanent upgrades in the shop",
            "Enemies spawn from open path edges. Kill them to earn gold for more towers and paths!",
            "You can rebuild over existing paths, but not over mines. Towers on replaced paths are removed and refunded",
            "Switch to the Towers tab",
            "Select a tower to place",
            "Tap near the path to place your tower",
            "Tap Start Wave to send enemies!",
            "Your tower attacks automatically. You're ready!"
        };

        [Header("UI References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private GraphicRaycaster raycaster;
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private Text messageText;
        [SerializeField] private Button okButton;
        [SerializeField] private Image arrowImage;
        [SerializeField] private RectTransform arrowRect;
        [SerializeField] private RectTransform safeAreaRect;

        private TutorialStep currentStep;

        private PieceDragHandler pieceDragHandler;
        private PieceHandUI pieceHandUI;
        private TowerManager towerManager;
        private HUDController hudController;
        private CameraController cameraController;
        private Camera mainCamera;

        private bool isTrackingWorldPosition;
        private Vector3 trackedWorldPos;
        private Vector3 arrowBounceDir;

        private bool isTrackingUITarget;
        private RectTransform trackedUITarget;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private static bool IsStepSeen(TutorialStep step)
        {
            return PlayerPrefs.GetInt(PrefPrefix + step, 0) == 1;
        }

        private static void MarkStepSeen(TutorialStep step)
        {
            PlayerPrefs.SetInt(PrefPrefix + step, 1);
        }

        private TutorialStep FindFirstUnseenStep()
        {
            for (int i = 0; i < StepCount; i++)
            {
                var step = (TutorialStep)i;
                if (ShouldSkipStep(step))
                    continue;
                if (!IsStepSeen(step))
                    return step;
            }
            return TutorialStep.Done;
        }

        private bool ShouldSkipStep(TutorialStep step)
        {
            // Skip tower steps if no towers are unlocked
            if (step == TutorialStep.SwitchToTowers ||
                step == TutorialStep.SelectTower ||
                step == TutorialStep.PlaceTower ||
                step == TutorialStep.StartWave)
            {
                if (!HasUnlockedTowers())
                    return true;
            }
            return false;
        }

        private bool HasUnlockedTowers()
        {
            if (!GameManager.Instance.UseFreeTowerPlacement)
                return false;
            var tm = FindFirstObjectByType<TowerManager>();
            if (tm == null || tm.AllTowers == null || tm.AllTowers.Count == 0)
                return false;
            for (int i = 0; i < tm.AllTowers.Count; i++)
            {
                if (LabManager.Instance == null || LabManager.Instance.IsTowerUnlocked(tm.AllTowers[i].towerName))
                    return true;
            }
            return false;
        }

        private bool subscribed;

        private void Start()
        {
            var firstUnseen = FindFirstUnseenStep();
            if (firstUnseen == TutorialStep.Done)
            {
                Debug.Log("TutorialManager: all steps seen, destroying.");
                Destroy(gameObject);
                return;
            }

            Debug.Log($"TutorialManager: resuming from {firstUnseen}.");

            if (okButton != null)
                okButton.onClick.AddListener(OnOKPressed);

            panel.SetActive(false);
            arrowImage.gameObject.SetActive(false);
            if (raycaster != null)
                raycaster.enabled = false;
            currentStep = firstUnseen;

            // Delay to ensure GameManager has created PieceDragHandler etc.
            Invoke(nameof(LateInit), 0.5f);
        }

        private void LateInit()
        {
            pieceDragHandler = FindFirstObjectByType<PieceDragHandler>();
            pieceHandUI = FindFirstObjectByType<PieceHandUI>();
            towerManager = FindFirstObjectByType<TowerManager>();
            hudController = FindFirstObjectByType<HUDController>();
            cameraController = FindFirstObjectByType<CameraController>();
            mainCamera = Camera.main;

            Subscribe();
            subscribed = true;

            ShowCurrentStep();
        }

        private void Subscribe()
        {
            if (pieceHandUI != null)
            {
                pieceHandUI.OnCardSelected += OnCardSelected;
                pieceHandUI.OnTowerCardSelected += OnTowerCardSelected;
                pieceHandUI.OnTabSwitched += OnTabSwitched;
            }
            if (pieceDragHandler != null)
                pieceDragHandler.OnPiecePlaced += OnPiecePlaced;
            if (towerManager != null)
                towerManager.OnTowerPlaced += OnTowerPlaced;
            if (GameManager.Instance != null)
                GameManager.Instance.OnWaveChanged += OnWaveStarted;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (!subscribed) return;

            if (pieceHandUI != null)
            {
                pieceHandUI.OnCardSelected -= OnCardSelected;
                pieceHandUI.OnTowerCardSelected -= OnTowerCardSelected;
                pieceHandUI.OnTabSwitched -= OnTabSwitched;
            }
            if (pieceDragHandler != null)
                pieceDragHandler.OnPiecePlaced -= OnPiecePlaced;
            if (towerManager != null)
                towerManager.OnTowerPlaced -= OnTowerPlaced;
            if (GameManager.Instance != null)
                GameManager.Instance.OnWaveChanged -= OnWaveStarted;
        }

        // --- Gate methods ---

        public bool AllowCardSelect(int index)
        {
            if (currentStep == TutorialStep.SelectPathCard)
                return index == 0;
            if (currentStep == TutorialStep.SelectTower)
                return true; // any tower card
            // Block card selection during non-card steps
            if (currentStep == TutorialStep.SwitchToTowers ||
                currentStep == TutorialStep.PlaceTower ||
                currentStep == TutorialStep.StartWave ||
                currentStep == TutorialStep.MineExplanation ||
                currentStep == TutorialStep.SpawnExplanation ||
                currentStep == TutorialStep.PathEditInfo ||
                currentStep == TutorialStep.Complete)
                return false;
            return true;
        }

        public bool AllowTabSwitch(int tabIndex)
        {
            if (currentStep == TutorialStep.SwitchToTowers)
                return tabIndex == 1; // Towers tab
            // Block tab switching during other blocking steps
            if (currentStep == TutorialStep.SelectPathCard ||
                currentStep == TutorialStep.BuildOnOre ||
                currentStep == TutorialStep.SelectTower ||
                currentStep == TutorialStep.PlaceTower ||
                currentStep == TutorialStep.StartWave ||
                currentStep == TutorialStep.MineExplanation ||
                currentStep == TutorialStep.SpawnExplanation ||
                currentStep == TutorialStep.PathEditInfo ||
                currentStep == TutorialStep.Complete)
                return false;
            return true;
        }

        public bool AllowGhostInteract(HexCoord coord)
        {
            if (currentStep == TutorialStep.BuildOnOre)
            {
                // Only allow placing on ore deposits
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    var patch = gm.GetOrePatchAt(coord);
                    return patch.HasValue;
                }
                return false;
            }
            // Block ghost interaction during non-placement steps
            if (currentStep == TutorialStep.SelectPathCard ||
                currentStep == TutorialStep.SwitchToTowers ||
                currentStep == TutorialStep.SelectTower ||
                currentStep == TutorialStep.StartWave ||
                currentStep == TutorialStep.MineExplanation ||
                currentStep == TutorialStep.SpawnExplanation ||
                currentStep == TutorialStep.PathEditInfo ||
                currentStep == TutorialStep.Complete)
                return false;
            return true;
        }

        public bool AllowTowerPlace()
        {
            if (currentStep == TutorialStep.PlaceTower)
                return true;
            // Block during non-tower steps
            if (currentStep == TutorialStep.SelectPathCard ||
                currentStep == TutorialStep.BuildOnOre ||
                currentStep == TutorialStep.SwitchToTowers ||
                currentStep == TutorialStep.SelectTower ||
                currentStep == TutorialStep.StartWave ||
                currentStep == TutorialStep.MineExplanation ||
                currentStep == TutorialStep.SpawnExplanation ||
                currentStep == TutorialStep.PathEditInfo ||
                currentStep == TutorialStep.Complete)
                return false;
            return true;
        }

        // --- Step display ---

        private void ShowCurrentStep()
        {
            ShowStep(currentStep);
        }

        private void ShowStep(TutorialStep step)
        {
            currentStep = step;

            if (step == TutorialStep.Done)
            {
                CompleteTutorial();
                return;
            }

            MarkStepSeen(step);

            messageText.text = StepMessages[(int)step];
            panel.SetActive(true);

            bool isInfoStep = IsInfoStep(step);
            okButton.gameObject.SetActive(isInfoStep);

            if (isInfoStep)
            {
                // Info steps: show raycaster so OK button is clickable
                if (raycaster != null)
                    raycaster.enabled = true;
            }
            else
            {
                // Action steps: disable raycaster so input passes through to game
                if (raycaster != null)
                    raycaster.enabled = false;
            }

            isTrackingWorldPosition = false;
            isTrackingUITarget = false;

            PositionPanel(step);
            UpdateArrow(step);
        }

        private bool IsInfoStep(TutorialStep step)
        {
            return step == TutorialStep.MineExplanation ||
                   step == TutorialStep.SpawnExplanation ||
                   step == TutorialStep.PathEditInfo ||
                   step == TutorialStep.Complete;
        }

        private void PositionPanel(TutorialStep step)
        {
            switch (step)
            {
                // Card/tab steps — center-left, near hand panel
                case TutorialStep.SelectPathCard:
                case TutorialStep.SwitchToTowers:
                case TutorialStep.SelectTower:
                    panelRect.anchorMin = new Vector2(0.35f, 0.5f);
                    panelRect.anchorMax = new Vector2(0.35f, 0.5f);
                    panelRect.anchoredPosition = Vector2.zero;
                    break;

                // HUD button steps — center-right, near start wave button
                case TutorialStep.StartWave:
                    panelRect.anchorMin = new Vector2(0.65f, 0.3f);
                    panelRect.anchorMax = new Vector2(0.65f, 0.3f);
                    panelRect.anchoredPosition = Vector2.zero;
                    break;

                // World steps — top-center
                case TutorialStep.BuildOnOre:
                case TutorialStep.PlaceTower:
                    panelRect.anchorMin = new Vector2(0.5f, 0.85f);
                    panelRect.anchorMax = new Vector2(0.5f, 0.85f);
                    panelRect.anchoredPosition = Vector2.zero;
                    break;

                // Info steps — centered
                default:
                    panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                    panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                    panelRect.anchoredPosition = Vector2.zero;
                    break;
            }
        }

        private void UpdateArrow(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.SelectPathCard:
                    PointArrowAtUI(pieceHandUI?.GetCardRectTransform(0), Vector2.right);
                    break;

                case TutorialStep.BuildOnOre:
                    var oreCoord = GameManager.Instance?.GetGuaranteedOreDeposit();
                    if (oreCoord.HasValue)
                    {
                        PanToOreDeposit(oreCoord.Value);
                        PointArrowAtWorldPos(HexGrid.HexToWorld(oreCoord.Value) + Vector3.up * 5f, Vector2.down);
                    }
                    else
                    {
                        arrowImage.gameObject.SetActive(false);
                    }
                    break;

                case TutorialStep.SwitchToTowers:
                    PointArrowAtUI(pieceHandUI?.GetTowersTabRectTransform(), Vector2.right);
                    break;

                case TutorialStep.SelectTower:
                    PointArrowAtUI(pieceHandUI?.GetCardRectTransform(0), Vector2.right);
                    break;

                case TutorialStep.PlaceTower:
                    // Point near a placed path tile
                    var nearPathCoord = FindNearPathCoord();
                    if (nearPathCoord.HasValue)
                        PointArrowAtWorldPos(HexGrid.HexToWorld(nearPathCoord.Value) + Vector3.up * 2f, Vector2.down);
                    else
                        arrowImage.gameObject.SetActive(false);
                    break;

                case TutorialStep.StartWave:
                    PointArrowAtUI(hudController?.GetStartWaveButtonRectTransform(), Vector2.down);
                    break;

                default:
                    arrowImage.gameObject.SetActive(false);
                    break;
            }
        }

        private HexCoord? FindNearPathCoord()
        {
            var gm = GameManager.Instance;
            if (gm == null) return null;

            HexCoord? best = null;
            int bestDist = int.MaxValue;
            var castle = new HexCoord(0, 0);

            foreach (var kvp in gm.MapData)
            {
                if (kvp.Value.IsCastle) continue;
                if (kvp.Value.ConnectedEdges.Count == 0) continue;
                int dist = castle.DistanceTo(kvp.Key);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = kvp.Key;
                }
            }
            return best;
        }

        private void PointArrowAtUI(RectTransform target, Vector2 bounceDir)
        {
            if (target == null)
            {
                arrowImage.gameObject.SetActive(false);
                return;
            }

            arrowImage.gameObject.SetActive(true);
            isTrackingUITarget = true;
            trackedUITarget = target;
            arrowBounceDir = new Vector3(bounceDir.x, bounceDir.y, 0f);

            // Rotate arrow to face TOWARD the target (opposite of bounce offset)
            float angle = Mathf.Atan2(-bounceDir.y, -bounceDir.x) * Mathf.Rad2Deg;
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            UpdateUIArrowPosition();
        }

        private void UpdateUIArrowPosition()
        {
            if (trackedUITarget == null) return;

            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, trackedUITarget.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                safeAreaRect, screenPos, null, out Vector2 localPoint))
            {
                // Offset the arrow away from the target
                float bounce = Mathf.Sin(Time.unscaledTime * 3f) * 15f;
                Vector2 offset = new Vector2(arrowBounceDir.x, arrowBounceDir.y) * (40f + bounce);
                arrowRect.anchoredPosition = localPoint + offset;
                arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
                arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            }
        }

        private void PointArrowAtWorldPos(Vector3 worldPos, Vector2 bounceDir)
        {
            arrowImage.gameObject.SetActive(true);
            isTrackingWorldPosition = true;
            trackedWorldPos = worldPos;
            arrowBounceDir = new Vector3(bounceDir.x, bounceDir.y, 0f);

            // Rotate arrow to face TOWARD the target (opposite of bounce offset)
            float angle = Mathf.Atan2(-bounceDir.y, -bounceDir.x) * Mathf.Rad2Deg;
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            UpdateWorldArrowPosition();
        }

        private void UpdateWorldArrowPosition()
        {
            if (mainCamera == null) return;

            Vector3 screenPos = mainCamera.WorldToScreenPoint(trackedWorldPos);
            if (screenPos.z < 0f)
            {
                arrowImage.gameObject.SetActive(false);
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                safeAreaRect, screenPos, null, out Vector2 localPoint))
            {
                float bounce = Mathf.Sin(Time.unscaledTime * 3f) * 15f;
                Vector2 offset = new Vector2(arrowBounceDir.x, arrowBounceDir.y) * (40f + bounce);
                arrowRect.anchoredPosition = localPoint + offset;
                arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
                arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            }
        }

        private void Update()
        {
            if (isTrackingWorldPosition)
                UpdateWorldArrowPosition();
            else if (isTrackingUITarget)
                UpdateUIArrowPosition();
        }

        // --- Camera panning ---

        private void PanToOreDeposit(HexCoord coord)
        {
            if (cameraController == null) return;
            Vector3 depositWorld = HexGrid.HexToWorld(coord);
            cameraController.PanToPosition(depositWorld);
        }

        // --- Event handlers ---

        private void OnCardSelected(int index, HexPieceType type)
        {
            if (currentStep == TutorialStep.SelectPathCard)
            {
                AdvanceTo(TutorialStep.BuildOnOre);
            }
            else if (currentStep == TutorialStep.SelectTower)
            {
                AdvanceTo(TutorialStep.PlaceTower);
            }
        }

        private void OnTowerCardSelected(TowerData data)
        {
            if (currentStep == TutorialStep.SelectTower)
            {
                AdvanceTo(TutorialStep.PlaceTower);
            }
        }

        private void OnTabSwitched(int tabIndex)
        {
            if (currentStep == TutorialStep.SwitchToTowers && tabIndex == 1)
            {
                AdvanceTo(TutorialStep.SelectTower);
            }
        }

        private void OnPiecePlaced(int handIndex, PlacementRotation rotation, HexCoord coord)
        {
            if (currentStep == TutorialStep.BuildOnOre)
            {
                AdvanceTo(TutorialStep.MineExplanation);
            }
        }

        private void OnTowerPlaced(Tower tower)
        {
            if (currentStep == TutorialStep.PlaceTower)
            {
                AdvanceTo(TutorialStep.StartWave);
            }
        }

        private void OnWaveStarted(int wave)
        {
            if (currentStep == TutorialStep.StartWave)
            {
                AdvanceTo(TutorialStep.Complete);
            }
        }

        private void OnOKPressed()
        {
            AdvanceFromInfo();
        }

        private void AdvanceFromInfo()
        {
            switch (currentStep)
            {
                case TutorialStep.MineExplanation:
                    AdvanceTo(TutorialStep.SpawnExplanation);
                    break;
                case TutorialStep.SpawnExplanation:
                    AdvanceTo(TutorialStep.PathEditInfo);
                    break;
                case TutorialStep.PathEditInfo:
                    AdvanceToNextActionStep(TutorialStep.PathEditInfo);
                    break;
                case TutorialStep.Complete:
                    AdvanceTo(TutorialStep.Done);
                    break;
            }
        }

        private void AdvanceTo(TutorialStep next)
        {
            HideAll();

            // Skip steps that should be skipped
            while (next != TutorialStep.Done && ShouldSkipStep(next))
            {
                MarkStepSeen(next);
                next = (TutorialStep)((int)next + 1);
            }

            ShowStep(next);
        }

        private void AdvanceToNextActionStep(TutorialStep after)
        {
            var next = (TutorialStep)((int)after + 1);
            while (next != TutorialStep.Done && ShouldSkipStep(next))
            {
                MarkStepSeen(next);
                next = (TutorialStep)((int)next + 1);
            }
            if ((int)next >= StepCount)
                next = TutorialStep.Done;

            HideAll();
            ShowStep(next);
        }

        // --- Utility ---

        private void HideAll()
        {
            panel.SetActive(false);
            arrowImage.gameObject.SetActive(false);
            isTrackingWorldPosition = false;
            isTrackingUITarget = false;
            if (raycaster != null)
                raycaster.enabled = false;
        }

        private void CompleteTutorial()
        {
            HideAll();
            Debug.Log("Tutorial complete!");
            Destroy(gameObject);
        }
    }
}
