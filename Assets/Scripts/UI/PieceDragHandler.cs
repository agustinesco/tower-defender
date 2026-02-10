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

        // Selected card state
        private bool isCardSelected;
        private int selectedHandIndex;
        private HexPieceType selectedPieceType;

        // Ghost press/hold state
        private bool isPressingGhost;
        private HexCoord pressedGhostCoord;
        private float pressStartTime;
        private const float HoldDuration = 0.5f;

        // Modification state
        private bool isModificationSelected;
        private ModificationType selectedModification;
        private HashSet<HexCoord> validModificationTargets = new HashSet<HexCoord>();
        private Dictionary<HexCoord, Color> originalHexColors = new Dictionary<HexCoord, Color>();

        // Tower placement state (free mode)
        private bool isTowerSelected;
        private bool isTowerDragging;
        private TowerData selectedTowerData;
        private GameObject towerGhost;
        private GameObject towerRadiusIndicator;
        private Vector3 lastSnappedPosition;
        private bool lastSnapValid;
        private const float PerpendicularOffset = 6.5f; // Same as TowerSlot generation

        private static readonly Color HighlightColor = new Color(0.3f, 0.9f, 0.3f);

        public bool IsInteracting => isPressingGhost || isTowerDragging;
        public bool IsPlacingPiece => isCardSelected || isModificationSelected || isTowerSelected;

        public event Action<int, PlacementRotation, HexCoord> OnPiecePlaced;

        public void Initialize(PlacementValidator validator, GhostPieceManager ghostManager, PieceHandUI handUI)
        {
            this.validator = validator;
            this.ghostManager = ghostManager;
            this.handUI = handUI;
            cam = Camera.main;

            handUI.OnCardSelected += OnCardSelected;
            handUI.OnCardDeselected += OnCardDeselected;
            handUI.OnModificationSelected += OnModificationSelected;
            handUI.OnModificationDeselected += OnModificationDeselected;
            handUI.OnTowerCardSelected += OnTowerCardSelected;
            handUI.OnTowerCardDeselected += OnTowerCardDeselected;
        }

        private void OnDestroy()
        {
            if (handUI != null)
            {
                handUI.OnCardSelected -= OnCardSelected;
                handUI.OnCardDeselected -= OnCardDeselected;
                handUI.OnModificationSelected -= OnModificationSelected;
                handUI.OnModificationDeselected -= OnModificationDeselected;
                handUI.OnTowerCardSelected -= OnTowerCardSelected;
                handUI.OnTowerCardDeselected -= OnTowerCardDeselected;
            }
            DestroyTowerGhost();
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
            isTowerDragging = true;
            selectedTowerData = towerData;
            CreateTowerGhost(towerData);
        }

        private void OnTowerCardDeselected()
        {
            ClearTowerSelection();
        }

        private void ClearTowerSelection()
        {
            isTowerSelected = false;
            isTowerDragging = false;
            selectedTowerData = null;
            DestroyTowerGhost();
        }

        private void CreateTowerGhost(TowerData data)
        {
            DestroyTowerGhost();

            towerGhost = new GameObject("TowerGhost");

            // Base cylinder
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.transform.SetParent(towerGhost.transform);
            baseObj.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            baseObj.transform.localScale = new Vector3(2.4f, 0.75f, 2.4f);
            var bc = baseObj.GetComponent<Collider>();
            if (bc != null) Destroy(bc);
            var br = baseObj.GetComponent<Renderer>();
            if (br != null)
                br.material = MaterialCache.CreateTransparent(data.towerColor, 0.4f);

            // Head cube
            var headObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headObj.transform.SetParent(towerGhost.transform);
            headObj.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            headObj.transform.localScale = new Vector3(1.2f, 1.2f, 1.8f);
            var hc = headObj.GetComponent<Collider>();
            if (hc != null) Destroy(hc);
            var hr = headObj.GetComponent<Renderer>();
            if (hr != null)
                hr.material = MaterialCache.CreateTransparent(data.towerColor * 0.7f, 0.4f);

            // Radius indicator
            towerRadiusIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            towerRadiusIndicator.transform.SetParent(towerGhost.transform);
            towerRadiusIndicator.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            towerRadiusIndicator.transform.localScale = new Vector3(data.placementRadius * 2f, 0.01f, data.placementRadius * 2f);
            var rc = towerRadiusIndicator.GetComponent<Collider>();
            if (rc != null) Destroy(rc);
            var rr = towerRadiusIndicator.GetComponent<Renderer>();
            if (rr != null)
                rr.material = MaterialCache.CreateTransparent(new Color(1f, 1f, 0f, 0.15f), 0.15f);

            towerGhost.SetActive(false);
        }

        private void DestroyTowerGhost()
        {
            if (towerGhost != null)
            {
                Destroy(towerGhost);
                towerGhost = null;
                towerRadiusIndicator = null;
            }
        }

        private void SetGhostColor(bool valid)
        {
            if (towerGhost == null) return;
            Color tint = valid ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f);
            var renderers = towerGhost.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].gameObject == towerRadiusIndicator) continue;
                Color c = renderers[i].material.color;
                renderers[i].material.color = new Color(tint.r, tint.g, tint.b, c.a);
            }
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

                if (type == ModificationType.Mine)
                {
                    if (gm.GetOrePatchAt(coord) != null && !gm.HasMine(coord))
                        validModificationTargets.Add(coord);
                }
                else if (type == ModificationType.Lure)
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
            if (cam == null || !isTowerDragging) return;

            // Raycast ground plane (y=0) for cursor world position
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float enter))
            {
                if (towerGhost != null) towerGhost.SetActive(false);
                lastSnapValid = false;
                return;
            }

            Vector3 cursorWorld = ray.GetPoint(enter);

            // Snap to nearest path
            Vector3 snappedPos;
            bool valid = TrySnapToPath(cursorWorld, selectedTowerData, out snappedPos);

            // Show and move ghost to snapped position
            if (towerGhost != null)
            {
                towerGhost.SetActive(true);
                towerGhost.transform.position = valid ? snappedPos : cursorWorld;
            }

            lastSnapValid = valid;
            lastSnappedPosition = snappedPos;
            SetGhostColor(valid);

            // Place tower on finger lift, deselect on cancel
            if (Input.GetMouseButtonUp(0))
            {
                if (valid)
                {
                    var towerManager = FindFirstObjectByType<TowerManager>();
                    if (towerManager != null && towerManager.PlaceTowerAt(selectedTowerData, snappedPos))
                    {
                        ClearTowerSelection();
                        handUI.DeselectTowerCard();
                        return;
                    }
                }
                // Invalid position or placement failed - deselect
                ClearTowerSelection();
                handUI.DeselectTowerCard();
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
            var towerManager = FindFirstObjectByType<TowerManager>();
            if (towerManager != null && towerManager.IsTooCloseToOtherTower(snappedPos, towerData.placementRadius))
                return false;

            return true;
        }

        private void HandleModificationInput()
        {
            if (cam == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

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
                        if (selectedModification == ModificationType.Mine)
                            success = gm.BuildMine(nearest);
                        else if (selectedModification == ModificationType.Lure)
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

        private void HandleGhostInput()
        {
            if (cam == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    var ghost = hit.collider.GetComponent<HexPiece>();
                    if (ghost != null && ghost.IsGhost)
                    {
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
                float progress = Mathf.Clamp01(elapsed / HoldDuration);
                ghostManager.UpdateHoldProgress(pressedGhostCoord, progress);

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
