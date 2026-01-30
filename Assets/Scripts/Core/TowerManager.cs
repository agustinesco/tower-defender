using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TowerDefense.Entities;
using TowerDefense.Grid;

namespace TowerDefense.Core
{
    public class TowerManager : MonoBehaviour
    {
        [SerializeField] private List<TowerData> availableTowers;

        private TowerSlot selectedSlot;
        private Tower selectedTower;

        public TowerSlot SelectedSlot => selectedSlot;
        public Tower SelectedTower => selectedTower;
        public List<TowerData> AvailableTowers => availableTowers;

        public event System.Action<TowerSlot> OnSlotSelected;
        public event System.Action<Tower> OnTowerSelected;
        public event System.Action OnSelectionCleared;

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
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
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                // Check if we hit a tower slot
                var slot = hit.collider.GetComponent<TowerSlot>();
                if (slot != null)
                {
                    SelectSlot(slot);
                    return;
                }

                // Check if we hit a tower (through its slot)
                var slotParent = hit.collider.GetComponentInParent<TowerSlot>();
                if (slotParent != null && slotParent.IsOccupied)
                {
                    SelectTower(slotParent.CurrentTower);
                    return;
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
            tower.Initialize(towerData);

            selectedSlot.PlaceTower(tower);
            ClearSelection();

            return true;
        }

        public bool SellTower()
        {
            if (selectedTower == null)
                return false;

            int sellValue = selectedTower.SellValue;
            var slot = selectedTower.GetComponentInParent<TowerSlot>();

            if (slot != null)
            {
                slot.RemoveTower();
            }

            Destroy(selectedTower.gameObject);
            GameManager.Instance.AddCurrency(sellValue);
            ClearSelection();

            return true;
        }
    }
}