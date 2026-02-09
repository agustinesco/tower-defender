using System;
using System.Collections.Generic;
using UnityEngine;
using TowerDefense.Core;
using TowerDefense.Grid;

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

        private static readonly Color HighlightColor = new Color(0.3f, 0.9f, 0.3f);

        public bool IsInteracting => isPressingGhost;
        public bool IsPlacingPiece => isCardSelected || isModificationSelected;

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
        }

        private void OnDestroy()
        {
            if (handUI != null)
            {
                handUI.OnCardSelected -= OnCardSelected;
                handUI.OnCardDeselected -= OnCardDeselected;
                handUI.OnModificationSelected -= OnModificationSelected;
                handUI.OnModificationDeselected -= OnModificationDeselected;
            }
        }

        private void OnCardSelected(int index, HexPieceType type)
        {
            // Clear modification state if active
            if (isModificationSelected)
                ClearModificationHighlights();

            selectedHandIndex = index;
            selectedPieceType = type;
            isCardSelected = true;
            isPressingGhost = false;

            var placements = validator.GetValidPlacements(type);
            ghostManager.ShowGhosts(placements);
        }

        private void OnCardDeselected()
        {
            isCardSelected = false;
            isPressingGhost = false;
            ghostManager.HideAllGhosts();
        }

        private void OnModificationSelected(ModificationType type)
        {
            // Clear path card state if active
            if (isCardSelected)
            {
                isCardSelected = false;
                isPressingGhost = false;
                ghostManager.HideAllGhosts();
            }

            // Clear any existing modification highlights before showing new ones
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
            if (isModificationSelected)
            {
                HandleModificationInput();
                return;
            }

            if (!isCardSelected) return;

            HandleGhostInput();
        }

        private void HandleModificationInput()
        {
            if (cam == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                // Skip if over UI
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                // Raycast against ground plane (y=0) to find world position,
                // then find nearest valid hex coord (placed tiles have no colliders)
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

            // Mouse down — start press on ghost
            if (Input.GetMouseButtonDown(0))
            {
                // Skip if over UI
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

            // Mouse held — check if hold duration reached
            if (Input.GetMouseButton(0) && isPressingGhost)
            {
                float elapsed = Time.time - pressStartTime;
                if (elapsed >= HoldDuration)
                {
                    // Place the piece
                    var rotation = ghostManager.GetCurrentPlacement(pressedGhostCoord);
                    if (rotation != null)
                    {
                        var coord = pressedGhostCoord;
                        int handIndex = selectedHandIndex;

                        isPressingGhost = false;
                        isCardSelected = false;
                        ghostManager.HideAllGhosts();
                        handUI.Deselect();

                        OnPiecePlaced?.Invoke(handIndex, rotation, coord);
                    }
                    else
                    {
                        isPressingGhost = false;
                        ghostManager.ClearHighlight();
                    }
                }
            }

            // Mouse up — if released before hold, it's a tap (cycle rotation)
            if (Input.GetMouseButtonUp(0) && isPressingGhost)
            {
                float elapsed = Time.time - pressStartTime;
                isPressingGhost = false;

                if (elapsed < HoldDuration)
                {
                    ghostManager.CycleRotation(pressedGhostCoord);
                }

                ghostManager.ClearHighlight();
            }
        }
    }
}
