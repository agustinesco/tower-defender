using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TowerDefense.Entities;
using TowerDefense.Grid;
using TowerDefense.UI;

namespace TowerDefense.Core
{
    public class TowerManager : MonoBehaviour
    {
        [SerializeField] private List<TowerData> availableTowers;

        private TowerSlot selectedSlot;
        private Tower selectedTower;
        private PieceDragHandler pieceDragHandler;
        private List<Tower> placedTowers = new List<Tower>();
        private Camera cachedCamera;
        private Dictionary<HexCoord, List<Tower>> towersByTile = new Dictionary<HexCoord, List<Tower>>();

        public TowerSlot SelectedSlot => selectedSlot;
        public Tower SelectedTower => selectedTower;
        public List<TowerData> AvailableTowers
        {
            get
            {
                if (LabManager.Instance == null)
                    return availableTowers;

                var unlocked = new List<TowerData>();
                foreach (var tower in availableTowers)
                {
                    if (LabManager.Instance.IsTowerUnlocked(tower.towerName))
                        unlocked.Add(tower);
                }
                return unlocked;
            }
        }

        public event System.Action<TowerSlot> OnSlotSelected;
        public event System.Action<Tower> OnTowerSelected;
        public event System.Action OnSelectionCleared;

        private void Start()
        {
            cachedCamera = Camera.main;
        }

        public void SetPieceDragHandler(PieceDragHandler handler)
        {
            pieceDragHandler = handler;
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            // Skip tower placement input when placing pieces
            if (pieceDragHandler != null && pieceDragHandler.IsPlacingPiece)
                return;

            // Handle touch input
            if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                Touch touch = Input.GetTouch(0);

                // Don't process if touching UI
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    return;

                ProcessSelection(touch.position);
                return;
            }

            // Handle mouse input
            if (Input.GetMouseButtonDown(0))
            {
                // Don't process if clicking on UI
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                ProcessSelection(Input.mousePosition);
            }
        }

        private void ProcessSelection(Vector2 screenPosition)
        {
            if (cachedCamera == null) cachedCamera = Camera.main;
            if (cachedCamera == null) return;
            Ray ray = cachedCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                bool freeMode = GameManager.Instance != null && GameManager.Instance.UseFreeTowerPlacement;

                if (freeMode)
                {
                    // In free mode, detect Tower directly via its collider
                    var tower = hit.collider.GetComponent<Tower>();
                    if (tower != null)
                    {
                        SelectTower(tower);
                        return;
                    }
                }
                else
                {
                    // Slot mode: check if we hit a tower slot
                    var slot = hit.collider.GetComponent<TowerSlot>();
                    if (slot != null)
                    {
                        SelectSlot(slot);
                        return;
                    }

                    // Check if we hit a tower through its slot parent
                    var slotParent = hit.collider.GetComponentInParent<TowerSlot>();
                    if (slotParent != null && slotParent.IsOccupied)
                    {
                        SelectTower(slotParent.CurrentTower);
                        return;
                    }
                }
            }

            ClearSelection();
        }

        public void SelectSlot(TowerSlot slot)
        {
            ClearSelection();

            if (slot.IsOccupied)
            {
                SelectTower(slot.CurrentTower);
                return;
            }

            selectedSlot = slot;
            slot.SetHighlight(true);
            OnSlotSelected?.Invoke(slot);
        }

        public void SelectTower(Tower tower)
        {
            ClearSelection();
            selectedTower = tower;
            tower.ShowRange(true);
            OnTowerSelected?.Invoke(tower);
        }

        public void ClearSelection()
        {
            if (selectedSlot != null)
            {
                selectedSlot.SetHighlight(false);
                selectedSlot = null;
            }

            if (selectedTower != null)
            {
                selectedTower.ShowRange(false);
                selectedTower = null;
            }

            OnSelectionCleared?.Invoke();
        }

        public bool BuildTower(TowerData towerData)
        {
            if (selectedSlot == null || selectedSlot.IsOccupied)
                return false;

            if (!GameManager.Instance.SpendCurrency(towerData.cost))
                return false;

            GameObject towerObj = new GameObject($"Tower_{towerData.towerName}");
            var tower = towerObj.AddComponent<Tower>();

            // Place tower first so it's parented to the slot
            selectedSlot.PlaceTower(tower);
            // Then initialize (so GetComponentInParent<TowerSlot> works for shotgun facing)
            tower.Initialize(towerData);

            placedTowers.Add(tower);

            // Register in tile tracking (slot mode)
            if (tower.TileCoord.HasValue)
                RegisterTowerOnTile(tower, tower.TileCoord.Value);

            ClearSelection();

            return true;
        }

        // --- Free Placement Mode ---

        public bool PlaceTowerAt(TowerData towerData, Vector3 worldPos)
        {
            if (!GameManager.Instance.SpendCurrency(towerData.cost))
                return false;

            GameObject towerObj = new GameObject($"Tower_{towerData.towerName}");
            towerObj.transform.position = worldPos;

            var tower = towerObj.AddComponent<Tower>();
            tower.Initialize(towerData);

            // Compute facing direction toward nearest path segment
            HexCoord coord = HexGrid.WorldToHex(worldPos);
            Vector3 facingDir = GetFacingDirectionToPath(worldPos, coord);
            tower.SetFacingDirection(facingDir);
            tower.SetTileCoord(coord);

            // Add collider for click detection in free mode
            var collider = towerObj.AddComponent<SphereCollider>();
            collider.radius = 2f;

            RegisterTowerOnTile(tower, coord);
            placedTowers.Add(tower);

            // Apply haste if tile has it
            if (GameManager.Instance.HasHaste(coord))
                tower.SetHasteMultiplier(1.3f);

            return true;
        }

        private Vector3 GetFacingDirectionToPath(Vector3 worldPos, HexCoord coord)
        {
            var mapData = GameManager.Instance.MapData;
            if (!mapData.TryGetValue(coord, out var pieceData))
                return Vector3.forward;

            Vector3 hexCenter = HexGrid.HexToWorld(coord);
            Vector3 localPos = worldPos - hexCenter;
            float bestDist = float.MaxValue;
            Vector3 bestPoint = Vector3.zero;

            foreach (int edge in pieceData.ConnectedEdges)
            {
                Vector3 edgeDir = HexMeshGenerator.GetEdgeDirection(edge);
                Vector3 edgeEnd = edgeDir * HexGrid.InnerRadius;
                float dist = HexPiece.PointToSegmentDistance(localPos, Vector3.zero, edgeEnd);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    // Find closest point on segment for facing direction
                    Vector3 ab = edgeEnd;
                    float t = Mathf.Clamp01(Vector3.Dot(localPos, ab) / ab.sqrMagnitude);
                    bestPoint = ab * t;
                }
            }

            Vector3 dir = bestPoint - localPos;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
        }

        public void RegisterTowerOnTile(Tower tower, HexCoord coord)
        {
            if (!towersByTile.TryGetValue(coord, out var list))
            {
                list = new List<Tower>();
                towersByTile[coord] = list;
            }
            list.Add(tower);
        }

        public bool IsTooCloseToOtherTower(Vector3 pos, float radius)
        {
            for (int i = 0; i < placedTowers.Count; i++)
            {
                var tower = placedTowers[i];
                if (tower == null) continue;
                float minDist = Mathf.Max(radius, tower.Data != null ? tower.Data.placementRadius : 5f);
                if (Vector3.Distance(pos, tower.transform.position) < minDist)
                    return true;
            }
            return false;
        }

        public List<Tower> GetTowersOnTile(HexCoord coord)
        {
            if (towersByTile.TryGetValue(coord, out var list))
                return list;
            return null;
        }

        public int RemoveTowersOnTile(HexCoord coord)
        {
            int refund = 0;
            if (towersByTile.TryGetValue(coord, out var list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var tower = list[i];
                    if (tower != null)
                    {
                        refund += tower.SellValue;
                        placedTowers.Remove(tower);
                        Destroy(tower.gameObject);
                    }
                }
                list.Clear();
                towersByTile.Remove(coord);
            }
            return refund;
        }

        // --- Existing Methods (kept for slot mode) ---

        public int RemoveTowersOnPiece(HexPiece piece)
        {
            int refund = 0;
            foreach (var slot in piece.Slots)
            {
                if (slot.IsOccupied)
                {
                    refund += slot.CurrentTower.SellValue;
                    placedTowers.Remove(slot.CurrentTower);
                }
            }
            return refund;
        }

        public bool SellTower()
        {
            if (selectedTower == null)
                return false;

            int sellValue = selectedTower.SellValue;

            // Remove from tile tracking
            if (selectedTower.TileCoord.HasValue)
            {
                var coord = selectedTower.TileCoord.Value;
                if (towersByTile.TryGetValue(coord, out var list))
                {
                    list.Remove(selectedTower);
                    if (list.Count == 0)
                        towersByTile.Remove(coord);
                }
            }

            bool freeMode = GameManager.Instance != null && GameManager.Instance.UseFreeTowerPlacement;

            // In slot mode, clean up the slot before destroying the tower
            if (!freeMode)
            {
                var slot = selectedTower.GetComponentInParent<TowerSlot>();
                if (slot != null)
                    slot.RemoveTower();
            }

            placedTowers.Remove(selectedTower);
            Destroy(selectedTower.gameObject);

            GameManager.Instance.AddCurrency(sellValue);
            ClearSelection();

            return true;
        }
    }
}
