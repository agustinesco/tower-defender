using System.Collections.Generic;
using TMPro;
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

        private const int StepCount = 10; // Everything before Done

        private static readonly string[] StepMessages = new string[]
        {
            "Select a path card",
            "Place a path on the ore deposit to build a mine!",
            "Mine placed! It will gather resources over time while you play. At the end of each run, collected resources can be spent on permanent upgrades in the shop",
            "Enemies spawn from open path edges. Kill them to earn gold for more towers and paths!",
            "Enemies spawn from open path edges. Place towers near paths to defend! You can rebuild over existing paths, but not over mines",
            "Tap a slot near the path",
            "Select a tower to build",
            "Tap Build to confirm placement",
            "Tap Start Wave to send enemies!",
            "Your tower attacks automatically. You're ready!"
        };

        [Header("UI References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private GraphicRaycaster raycaster;
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button okButton;
        [SerializeField] private Image arrowImage;
        [SerializeField] private RectTransform arrowRect;
        [SerializeField] private RectTransform safeAreaRect;

        public static Sprite CachedArrowSprite { get; private set; }

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

        private List<GameObject> tutorialSlotMarkers = new List<GameObject>();
        private bool selectPathCardPhaseTwo;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (arrowImage != null && arrowImage.sprite != null)
                CachedArrowSprite = arrowImage.sprite;
        }

        private static bool IsStepSeen(TutorialStep step)
        {
            return JsonSaveSystem.IsTutorialStepSeen(step.ToString());
        }

        private static void MarkStepSeen(TutorialStep step)
        {
            JsonSaveSystem.MarkTutorialStepSeen(step.ToString());
            JsonSaveSystem.Save();
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
            // SpawnExplanation merged into PathEditInfo
            if (step == TutorialStep.SpawnExplanation)
                return true;

            // Complete step removed
            if (step == TutorialStep.Complete)
                return true;

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
            // During SwitchToTowers, block direct tab taps — only slot tap triggers the switch
            if (currentStep == TutorialStep.SwitchToTowers)
                return false;
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
            // Allow placing during SelectPathCard phase two (card selected, placing tile)
            if (currentStep == TutorialStep.SelectPathCard && selectPathCardPhaseTwo)
                return true;

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

        public bool AllowRotation()
        {
            // Block rotation during early path placement steps
            if (currentStep == TutorialStep.SelectPathCard ||
                currentStep == TutorialStep.BuildOnOre)
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

            if (step == TutorialStep.SwitchToTowers)
                HighlightAllSlots(true);
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
                case TutorialStep.SwitchToTowers:
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
                    if (selectPathCardPhaseTwo)
                    {
                        var ghostPos = FindGhostTowardOre();
                        if (ghostPos.HasValue)
                            PointArrowAtWorldPos(ghostPos.Value + Vector3.up * 5f, Vector2.down);
                        else
                            arrowImage.gameObject.SetActive(false);
                    }
                    else
                    {
                        PointArrowAtUI(pieceHandUI?.GetCardRectTransform(0), Vector2.right);
                    }
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
                    // World-space markers on all slots are shown instead of a single arrow
                    arrowImage.gameObject.SetActive(false);
                    break;

                case TutorialStep.SelectTower:
                    PointArrowAtUI(pieceHandUI?.GetCardRectTransform(0), Vector2.right);
                    break;

                case TutorialStep.PlaceTower:
                    if (pieceDragHandler != null && pieceDragHandler.BuildButtonRect != null)
                    {
                        PointArrowAtUI(pieceDragHandler.BuildButtonRect, Vector2.down);
                    }
                    else
                    {
                        var slotPos = FindNearestAvailableSlot();
                        if (slotPos.HasValue)
                            PointArrowAtWorldPos(slotPos.Value + Vector3.up * 2f, Vector2.down);
                        else
                            arrowImage.gameObject.SetActive(false);
                    }
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

        private void HighlightAllSlots(bool highlighted)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            foreach (var kvp in gm.HexPieces)
            {
                foreach (var slot in kvp.Value.Slots)
                {
                    if (!slot.IsOccupied)
                        slot.SetHighlight(highlighted);
                }
            }

            if (highlighted)
                CreateSlotMarkers();
            else
                ClearSlotMarkers();
        }

        private void CreateSlotMarkers()
        {
            ClearSlotMarkers();
            var gm = GameManager.Instance;
            if (gm == null) return;

            foreach (var kvp in gm.HexPieces)
            {
                foreach (var slot in kvp.Value.Slots)
                {
                    if (slot.IsOccupied) continue;

                    var marker = MaterialCache.CreatePrimitive(PrimitiveType.Sphere);
                    marker.name = "TutorialSlotMarker";
                    marker.transform.SetParent(slot.transform);
                    marker.transform.localPosition = Vector3.up * 5f;
                    marker.transform.localScale = new Vector3(2f, 2f, 2f);

                    var rend = marker.GetComponent<Renderer>();
                    if (rend != null)
                        rend.material = MaterialCache.CreateUnlit(new Color(1f, 1f, 0f));

                    tutorialSlotMarkers.Add(marker);
                }
            }
        }

        private void ClearSlotMarkers()
        {
            for (int i = 0; i < tutorialSlotMarkers.Count; i++)
            {
                if (tutorialSlotMarkers[i] != null)
                    Destroy(tutorialSlotMarkers[i]);
            }
            tutorialSlotMarkers.Clear();
        }

        private Vector3? FindNearestGhostPosition()
        {
            var gm = GameManager.Instance;
            if (gm == null) return null;

            Vector3 castle = HexGrid.HexToWorld(new HexCoord(0, 0));
            float bestDist = float.MaxValue;
            Vector3? best = null;

            foreach (var kvp in gm.MapData)
            {
                foreach (int edge in kvp.Value.ConnectedEdges)
                {
                    var neighbor = kvp.Value.Coord.GetNeighbor(edge);
                    if (gm.MapData.ContainsKey(neighbor)) continue;

                    Vector3 pos = HexGrid.HexToWorld(neighbor);
                    float dist = Vector3.Distance(castle, pos);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = pos;
                    }
                }
            }
            return best;
        }

        private Vector3? FindGhostTowardOre()
        {
            var gm = GameManager.Instance;
            if (gm == null) return null;

            var oreCoord = gm.GetGuaranteedOreDeposit();
            if (!oreCoord.HasValue) return FindNearestGhostPosition();

            Vector3 oreWorld = HexGrid.HexToWorld(oreCoord.Value);
            float bestDist = float.MaxValue;
            Vector3? best = null;

            foreach (var kvp in gm.MapData)
            {
                foreach (int edge in kvp.Value.ConnectedEdges)
                {
                    var neighbor = kvp.Value.Coord.GetNeighbor(edge);
                    if (gm.MapData.ContainsKey(neighbor)) continue;

                    Vector3 pos = HexGrid.HexToWorld(neighbor);
                    float dist = Vector3.Distance(oreWorld, pos);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = pos;
                    }
                }
            }
            return best;
        }

        private Vector3? FindNearestAvailableSlot()
        {
            var gm = GameManager.Instance;
            if (gm == null) return null;

            Vector3 castle = HexGrid.HexToWorld(new HexCoord(0, 0));
            float bestDist = float.MaxValue;
            Vector3? best = null;

            foreach (var kvp in gm.HexPieces)
            {
                foreach (var slot in kvp.Value.Slots)
                {
                    if (slot.IsOccupied) continue;
                    float dist = Vector3.Distance(castle, slot.transform.position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = slot.transform.position;
                    }
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
            // During SwitchToTowers, detect slot taps directly (bypasses UI overlay blocking)
            if (currentStep == TutorialStep.SwitchToTowers)
            {
                HandleSlotTapForTutorial();
                BobSlotMarkers();
            }

            // During PlaceTower, dynamically switch arrow to build button once it appears
            if (currentStep == TutorialStep.PlaceTower && isTrackingWorldPosition
                && pieceDragHandler != null && pieceDragHandler.BuildButtonRect != null)
            {
                PointArrowAtUI(pieceDragHandler.BuildButtonRect, Vector2.down);
            }

            if (isTrackingWorldPosition)
                UpdateWorldArrowPosition();
            else if (isTrackingUITarget)
                UpdateUIArrowPosition();
        }

        private void HandleSlotTapForTutorial()
        {
            if (mainCamera == null || towerManager == null) return;

            bool tapped = false;
            Vector2 tapPos = Vector2.zero;

            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                tapped = true;
                tapPos = Input.GetTouch(0).position;
            }
            else if (Input.GetMouseButtonDown(0))
            {
                tapped = true;
                tapPos = Input.mousePosition;
            }

            if (!tapped) return;

            Ray ray = mainCamera.ScreenPointToRay(tapPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                var slot = hit.collider.GetComponent<TowerSlot>()
                           ?? hit.collider.GetComponentInParent<TowerSlot>();
                if (slot != null && !slot.IsOccupied)
                {
                    // Select slot then force-switch to towers tab and advance
                    // (AllowTabSwitch blocks during SwitchToTowers so we force it)
                    towerManager.SelectSlot(slot);
                    if (pieceHandUI != null)
                        pieceHandUI.SwitchToTowersTab(force: true);
                    HighlightAllSlots(false);
                    AdvanceTo(TutorialStep.SelectTower);
                }
            }
        }

        private void BobSlotMarkers()
        {
            float bob = Mathf.Sin(Time.unscaledTime * 3f) * 1f;
            for (int i = 0; i < tutorialSlotMarkers.Count; i++)
            {
                if (tutorialSlotMarkers[i] != null)
                    tutorialSlotMarkers[i].transform.localPosition = new Vector3(0f, 5f + bob, 0f);
            }
        }

        // --- Camera panning ---

        private void PanToOreDeposit(HexCoord coord)
        {
            if (cameraController == null) return;
            Vector3 depositWorld = HexGrid.HexToWorld(coord);
            cameraController.ExpandBoundsToInclude(new List<Vector3> { depositWorld }, 30f);
            cameraController.PanToPosition(depositWorld);
        }

        // --- Event handlers ---

        private void OnCardSelected(int index, HexPieceType type)
        {
            if (currentStep == TutorialStep.SelectPathCard && !selectPathCardPhaseTwo)
            {
                // Phase 2: card selected, now guide to place a tile
                selectPathCardPhaseTwo = true;
                messageText.text = "Hold on a tile to place your path";
                panelRect.anchorMin = new Vector2(0.5f, 0.85f);
                panelRect.anchorMax = new Vector2(0.5f, 0.85f);
                panelRect.anchoredPosition = Vector2.zero;
                UpdateArrow(currentStep);

                // Pan camera toward the ghost closest to ore
                var ghostPos = FindGhostTowardOre();
                if (ghostPos.HasValue && cameraController != null)
                {
                    cameraController.ExpandBoundsToInclude(new List<Vector3> { ghostPos.Value }, 30f);
                    cameraController.PanToPosition(ghostPos.Value);
                }

                return;
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
            // SwitchToTowers advancement is handled directly in HandleSlotTapForTutorial
        }

        private void OnPiecePlaced(int handIndex, PlacementRotation rotation, HexCoord coord)
        {
            if (currentStep == TutorialStep.SelectPathCard && selectPathCardPhaseTwo)
            {
                selectPathCardPhaseTwo = false;
                AdvanceTo(TutorialStep.BuildOnOre);
            }
            else if (currentStep == TutorialStep.BuildOnOre)
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
                    AdvanceTo(TutorialStep.PathEditInfo);
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
            ClearSlotMarkers();
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
