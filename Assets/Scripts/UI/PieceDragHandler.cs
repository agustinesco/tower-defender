using System;
using System.Collections.Generic;
using UnityEngine;
using TowerDefense.Core;
using TowerDefense.Grid;
using TowerDefense.Entities;

namespace TowerDefense.UI
{
    public class PieceDragHandler : MonoBehaviour
    {
        private PlacementValidator validator;
        private GhostPieceManager ghostManager;
        private PieceHandUI handUI;
        private Camera cam;
        private TowerManager cachedTowerManager;

        // Selected card state
        private bool isCardSelected;
        private int selectedHandIndex;
        private HexPieceType selectedPieceType;

        // Ghost press/hold state
        private bool isPressingGhost;
        private HexCoord pressedGhostCoord;
        private float pressStartTime;
        private const float HoldDuration = 0.5f;
        private const float HoldVisualDelay = 0.15f;

        // Modification state
        private bool isModificationSelected;
        private ModificationType selectedModification;
        private HashSet<HexCoord> validModificationTargets = new HashSet<HexCoord>();
        private Dictionary<HexCoord, Color> originalHexColors = new Dictionary<HexCoord, Color>();

        // Tower placement state (free mode)
        private bool isTowerSelected;
        private TowerData selectedTowerData;
        private const float PerpendicularOffset = 6.5f;
        private const float PathHalfWidth = 4.0f;

        // Tower tap delay — prevents accidental placement when dragging
        private bool towerTapPending;
        private float towerTapStartTime;
        private Vector2 towerTapStartPos;
        private const float TowerTapDelay = 0.15f;
        private const float TowerTapMaxDrift = 20f; // pixels

        // Tower placement visual helpers
        private List<GameObject> towerExclusionIndicators = new List<GameObject>();
        private List<GameObject> pathBorderLines = new List<GameObject>();

        private static readonly Color HighlightColor = new Color(0.3f, 0.9f, 0.3f);

        public bool IsInteracting => isPressingGhost;
        public bool IsPlacingPiece => isCardSelected || isModificationSelected || isTowerSelected;

        public event Action<int, PlacementRotation, HexCoord> OnPiecePlaced;

        public void Initialize(PlacementValidator validator, GhostPieceManager ghostManager, PieceHandUI handUI)
        {
            this.validator = validator;
            this.ghostManager = ghostManager;
            this.handUI = handUI;
            cam = Camera.main;
            cachedTowerManager = FindFirstObjectByType<TowerManager>();

            handUI.OnCardSelected += OnCardSelected;
            handUI.OnCardDeselected += OnCardDeselected;
            handUI.OnModificationSelected += OnModificationSelected;
            handUI.OnModificationDeselected += OnModificationDeselected;
            handUI.OnTowerCardSelected += OnTowerCardSelected;
            handUI.OnTowerCardDeselected += OnTowerCardDeselected;
        }

        private void OnDestroy()
        {
            HideTowerPlacementHelpers();

            if (handUI != null)
            {
                handUI.OnCardSelected -= OnCardSelected;
                handUI.OnCardDeselected -= OnCardDeselected;
                handUI.OnModificationSelected -= OnModificationSelected;
                handUI.OnModificationDeselected -= OnModificationDeselected;
                handUI.OnTowerCardSelected -= OnTowerCardSelected;
                handUI.OnTowerCardDeselected -= OnTowerCardDeselected;
            }
        }

        private void OnCardSelected(int index, HexPieceType type)
        {
            if (isModificationSelected)
                ClearModificationHighlights();
            if (isTowerSelected)
                ClearTowerSelection();

            selectedHandIndex = index;
            selectedPieceType = type;
            isCardSelected = true;
            isPressingGhost = false;

            var placements = validator.GetValidPlacements(type);
            ghostManager.ShowGhosts(placements);
            GameManager.Instance?.SetOreMarkersHighlighted(true);
        }

        private void OnCardDeselected()
        {
            isCardSelected = false;
            isPressingGhost = false;
            ghostManager.HideAllGhosts();
            GameManager.Instance?.SetOreMarkersHighlighted(false);
        }

        private void OnModificationSelected(ModificationType type)
        {
            if (isCardSelected)
            {
                isCardSelected = false;
                isPressingGhost = false;
                ghostManager.HideAllGhosts();
                GameManager.Instance?.SetOreMarkersHighlighted(false);
            }
            if (isTowerSelected)
                ClearTowerSelection();

            if (isModificationSelected)
                ClearModificationHighlights();

            isModificationSelected = true;
            selectedModification = type;
            ComputeValidTargets(type);
            HighlightValidTargets();
        }

        private void OnModificationDeselected()
        {
            ClearModificationHighlights();
        }

        private void OnTowerCardSelected(TowerData towerData)
        {
            if (isCardSelected)
            {
                isCardSelected = false;
                isPressingGhost = false;
                ghostManager.HideAllGhosts();
                GameManager.Instance?.SetOreMarkersHighlighted(false);
            }
            if (isModificationSelected)
                ClearModificationHighlights();

            isTowerSelected = true;
            selectedTowerData = towerData;
            ShowTowerPlacementHelpers();
        }

        private void OnTowerCardDeselected()
        {
            ClearTowerSelection();
        }

        private void ClearTowerSelection()
        {
            isTowerSelected = false;
            selectedTowerData = null;
            HideTowerPlacementHelpers();
        }

        private void ShowTowerPlacementHelpers()
        {
            HideTowerPlacementHelpers();
            ShowExclusionRadii();
            ShowPathBorders();
        }

        private void HideTowerPlacementHelpers()
        {
            for (int i = 0; i < towerExclusionIndicators.Count; i++)
            {
                if (towerExclusionIndicators[i] != null)
                    Destroy(towerExclusionIndicators[i]);
            }
            towerExclusionIndicators.Clear();

            for (int i = 0; i < pathBorderLines.Count; i++)
            {
                if (pathBorderLines[i] != null)
                    Destroy(pathBorderLines[i]);
            }
            pathBorderLines.Clear();
        }

        private void ShowExclusionRadii()
        {
            if (cachedTowerManager == null) return;

            var towers = cachedTowerManager.PlacedTowers;
            for (int i = 0; i < towers.Count; i++)
            {
                var tower = towers[i];
                if (tower == null) continue;

                var indicator = MaterialCache.CreatePrimitive(PrimitiveType.Cylinder);
                indicator.transform.position = tower.transform.position + Vector3.up * 0.02f;
                float radius = tower.Data != null ? tower.Data.placementRadius : 5f;
                indicator.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

                var rend = indicator.GetComponent<Renderer>();
                if (rend != null)
                    rend.material = MaterialCache.CreateTransparent(new Color(1f, 0.3f, 0.3f), 0.15f);

                towerExclusionIndicators.Add(indicator);
            }
        }

        private void ShowPathBorders()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            foreach (var kvp in gm.MapData)
            {
                var coord = kvp.Key;
                var data = kvp.Value;
                if (data.ConnectedEdges.Count == 0 || data.IsCastle) continue;

                Vector3 hexCenter = HexGrid.HexToWorld(coord);

                foreach (int edge in data.ConnectedEdges)
                {
                    Vector3 edgeDir = HexMeshGenerator.GetEdgeDirection(edge);
                    Vector3 edgeMid = edgeDir * HexGrid.InnerRadius;
                    Vector3 perp = new Vector3(-edgeDir.z, 0f, edgeDir.x);

                    // Left border line
                    pathBorderLines.Add(CreateBorderLine(
                        hexCenter + perp * PathHalfWidth + Vector3.up * 0.1f,
                        hexCenter + edgeMid + perp * PathHalfWidth + Vector3.up * 0.1f));

                    // Right border line
                    pathBorderLines.Add(CreateBorderLine(
                        hexCenter - perp * PathHalfWidth + Vector3.up * 0.1f,
                        hexCenter + edgeMid - perp * PathHalfWidth + Vector3.up * 0.1f));
                }
            }
        }

        private GameObject CreateBorderLine(Vector3 start, Vector3 end)
        {
            var obj = new GameObject("PathBorder");
            var lr = obj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.startWidth = 0.4f;
            lr.endWidth = 0.4f;
            lr.material = MaterialCache.CreateUnlit(new Color(0.4f, 0.9f, 1f));
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return obj;
        }

        private void ComputeValidTargets(ModificationType type)
        {
            validModificationTargets.Clear();

            var gm = GameManager.Instance;
            if (gm == null) return;

            foreach (var kvp in gm.MapData)
            {
                var coord = kvp.Key;
                var data = kvp.Value;

                if (type == ModificationType.Lure)
                {
                    if (!data.IsCastle && !gm.HasLure(coord))
                        validModificationTargets.Add(coord);
                }
                else if (type == ModificationType.Haste)
                {
                    if (!data.IsCastle && !gm.HasHaste(coord) && gm.TileHasTower(coord))
                        validModificationTargets.Add(coord);
                }
                else if (type == ModificationType.GoldenTouch)
                {
                    if (!data.IsCastle && !gm.HasGoldenTouch(coord))
                        validModificationTargets.Add(coord);
                }
            }
        }

        private void HighlightValidTargets()
        {
            originalHexColors.Clear();

            var gm = GameManager.Instance;
            if (gm == null) return;

            foreach (var coord in validModificationTargets)
            {
                if (gm.HexPieces.TryGetValue(coord, out var hexPiece))
                {
                    var renderer = hexPiece.GetComponentInChildren<MeshRenderer>();
                    if (renderer != null)
                    {
                        originalHexColors[coord] = renderer.material.color;
                        renderer.material.color = HighlightColor;
                    }
                }
            }
        }

        private void ClearModificationHighlights()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                foreach (var kvp in originalHexColors)
                {
                    if (gm.HexPieces.TryGetValue(kvp.Key, out var hexPiece))
                    {
                        var renderer = hexPiece.GetComponentInChildren<MeshRenderer>();
                        if (renderer != null)
                            renderer.material.color = kvp.Value;
                    }
                }
            }

            originalHexColors.Clear();
            validModificationTargets.Clear();
            isModificationSelected = false;
        }

        private void Update()
        {
            if (isTowerSelected)
            {
                HandleTowerPlacementInput();
                return;
            }

            if (isModificationSelected)
            {
                HandleModificationInput();
                return;
            }

            if (!isCardSelected) return;

            HandleGhostInput();
        }

        private void HandleTowerPlacementInput()
        {
            if (cam == null) return;

            // Tutorial gate
            var tut = TowerDefense.Core.TutorialManager.Instance;
            if (tut != null && !tut.AllowTowerPlace())
                return;

            // Start tracking tap on touch/click begin
            if (IsTapOnGameWorld())
            {
                towerTapPending = true;
                towerTapStartTime = Time.unscaledTime;
                towerTapStartPos = Input.mousePosition;
                return;
            }

            if (!towerTapPending) return;

            // Cancel if finger drifted too far (user is dragging the camera)
            float drift = Vector2.Distance((Vector2)Input.mousePosition, towerTapStartPos);
            if (drift > TowerTapMaxDrift)
            {
                towerTapPending = false;
                return;
            }

            // Cancel if finger was released before the delay
            bool held = Input.GetMouseButton(0) || Input.touchCount > 0;
            if (!held)
            {
                towerTapPending = false;
                return;
            }

            // Wait for the delay
            if (Time.unscaledTime - towerTapStartTime < TowerTapDelay) return;

            // Delay met and finger still near start — place the tower
            towerTapPending = false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float enter))
                return;

            Vector3 cursorWorld = ray.GetPoint(enter);

            if (TrySnapToPath(cursorWorld, selectedTowerData, out Vector3 snappedPos))
            {
                if (cachedTowerManager != null && cachedTowerManager.PlaceTowerAt(selectedTowerData, snappedPos))
                {
                    HideTowerPlacementHelpers();
                    isTowerSelected = false;
                    selectedTowerData = null;
                    handUI.DeselectTowerCard();
                }
            }
        }

        /// <summary>
        /// Snaps the cursor position to a valid tower position alongside the nearest path segment.
        /// Returns true if a valid snap was found, false if cursor is not near any path.
        /// </summary>
        private bool TrySnapToPath(Vector3 cursorWorld, TowerData towerData, out Vector3 snappedPos)
        {
            snappedPos = cursorWorld;

            var gm = GameManager.Instance;
            if (gm == null) return false;

            // Check the hex the cursor is over
            HexCoord coord = HexGrid.WorldToHex(cursorWorld);
            if (!gm.MapData.TryGetValue(coord, out var pieceData))
                return false;

            if (pieceData.IsCastle || pieceData.ConnectedEdges.Count == 0)
                return false;

            Vector3 hexCenter = HexGrid.HexToWorld(coord);
            Vector3 localCursor = cursorWorld - hexCenter;

            // Find the closest point on any path segment
            float bestDist = float.MaxValue;
            Vector3 bestPathPoint = Vector3.zero;
            Vector3 bestPerp = Vector3.zero;

            foreach (int edge in pieceData.ConnectedEdges)
            {
                Vector3 edgeDir = HexMeshGenerator.GetEdgeDirection(edge);
                Vector3 edgeEnd = edgeDir * HexGrid.InnerRadius;
                Vector3 perpendicular = new Vector3(-edgeDir.z, 0f, edgeDir.x);

                // Project cursor onto this path segment (center → edge midpoint)
                Vector3 ab = edgeEnd; // segA = zero, segB = edgeEnd
                float abLenSq = ab.sqrMagnitude;
                float t = (abLenSq > 0.0001f)
                    ? Mathf.Clamp(Vector3.Dot(localCursor, ab) / abLenSq, 0.25f, 0.85f)
                    : 0.5f;
                Vector3 pathPoint = ab * t;

                float dist = Vector3.Distance(localCursor, pathPoint);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPathPoint = pathPoint;
                    bestPerp = perpendicular;
                }
            }

            // Determine which side of the path the cursor is on
            float side = Vector3.Dot(localCursor - bestPathPoint, bestPerp);
            float sign = (side >= 0f) ? 1f : -1f;

            // Snap position: path point + perpendicular offset on cursor's side
            Vector3 localSnapped = bestPathPoint + bestPerp * (sign * PerpendicularOffset);
            snappedPos = hexCenter + localSnapped;
            snappedPos.y = 0f;

            // Validate: check the snapped position is still inside this hex (not too far out)
            float distFromCenter = new Vector2(localSnapped.x, localSnapped.z).magnitude;
            if (distFromCenter > HexGrid.InnerRadius * 1.1f)
                return false;

            // Also check clearance from ALL path segments (not just the closest)
            float pathHalfWidth = 4.0f;
            foreach (int edge in pieceData.ConnectedEdges)
            {
                Vector3 edgeDir = HexMeshGenerator.GetEdgeDirection(edge);
                Vector3 edgeEnd = edgeDir * HexGrid.InnerRadius;
                float clearance = HexPiece.PointToSegmentDistance(localSnapped, Vector3.zero, edgeEnd);
                if (clearance < pathHalfWidth + 1f)
                    return false;
            }

            // Check overlap with other towers
            if (cachedTowerManager != null && cachedTowerManager.IsTooCloseToOtherTower(snappedPos, towerData.placementRadius))
                return false;

            return true;
        }

        private void HandleModificationInput()
        {
            if (cam == null) return;

            if (IsTapOnGameWorld())
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                var groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 worldPoint = ray.GetPoint(enter);
                    HexCoord nearest = FindNearestValidTarget(worldPoint);
                    if (validModificationTargets.Contains(nearest))
                    {
                        var gm = GameManager.Instance;
                        if (gm == null) return;

                        bool success = false;
                        if (selectedModification == ModificationType.Lure)
                            success = gm.BuildLure(nearest);
                        else if (selectedModification == ModificationType.Haste)
                            success = gm.BuildHaste(nearest);
                        else if (selectedModification == ModificationType.GoldenTouch)
                            success = gm.BuildGoldenTouch(nearest);

                        if (success)
                        {
                            ClearModificationHighlights();
                            handUI.Deselect();
                        }
                    }
                }
            }
        }

        private HexCoord FindNearestValidTarget(Vector3 worldPoint)
        {
            HexCoord best = default;
            float bestDist = float.MaxValue;

            foreach (var coord in validModificationTargets)
            {
                Vector3 hexCenter = HexGrid.HexToWorld(coord);
                float dist = Vector3.Distance(worldPoint, hexCenter);
                if (dist < bestDist && dist <= HexGrid.OuterRadius)
                {
                    bestDist = dist;
                    best = coord;
                }
            }

            return best;
        }

        /// <summary>
        /// Returns true when the player taps/clicks the game world (not UI).
        /// On mobile, uses touch.fingerId for reliable IsPointerOverGameObject checks.
        /// </summary>
        private bool IsTapOnGameWorld()
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase != TouchPhase.Began) return false;
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    return false;
                return true;
            }

            if (!Input.GetMouseButtonDown(0)) return false;
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return false;
            return true;
        }

        private void HandleGhostInput()
        {
            if (cam == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (!IsTapOnGameWorld())
                    return;

                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    var ghost = hit.collider.GetComponent<HexPiece>();
                    if (ghost != null && ghost.IsGhost)
                    {
                        // Tutorial gate
                        var tut = TowerDefense.Core.TutorialManager.Instance;
                        if (tut != null && !tut.AllowGhostInteract(ghost.Data.Coord))
                            return;

                        pressedGhostCoord = ghost.Data.Coord;
                        pressStartTime = Time.time;
                        isPressingGhost = true;
                        ghostManager.HighlightGhost(pressedGhostCoord);
                    }
                }
            }

            if (Input.GetMouseButton(0) && isPressingGhost)
            {
                float elapsed = Time.time - pressStartTime;
                if (elapsed > HoldVisualDelay)
                {
                    float visualProgress = Mathf.Clamp01((elapsed - HoldVisualDelay) / (HoldDuration - HoldVisualDelay));
                    ghostManager.UpdateHoldProgress(pressedGhostCoord, visualProgress);
                }

                if (elapsed >= HoldDuration)
                {
                    ghostManager.UpdateHoldProgress(pressedGhostCoord, 0f);
                    var rotation = ghostManager.GetCurrentPlacement(pressedGhostCoord);
                    if (rotation != null)
                    {
                        var coord = pressedGhostCoord;
                        int handIndex = selectedHandIndex;
                        var pieceType = selectedPieceType;

                        isPressingGhost = false;
                        ghostManager.HideAllGhosts();

                        OnPiecePlaced?.Invoke(handIndex, rotation, coord);

                        var placements = validator.GetValidPlacements(pieceType);
                        if (placements.Count > 0)
                        {
                            ghostManager.ShowGhosts(placements);
                        }
                        else
                        {
                            isCardSelected = false;
                            handUI.Deselect();
                            GameManager.Instance?.SetOreMarkersHighlighted(false);
                        }
                    }
                    else
                    {
                        isPressingGhost = false;
                        ghostManager.ClearHighlight();
                    }
                }
            }

            if (Input.GetMouseButtonUp(0) && isPressingGhost)
            {
                float elapsed = Time.time - pressStartTime;
                isPressingGhost = false;
                ghostManager.UpdateHoldProgress(pressedGhostCoord, 0f);

                if (elapsed < HoldDuration)
                {
                    ghostManager.CycleRotation(pressedGhostCoord);
                }

                ghostManager.ClearHighlight();
            }
        }
    }
}
