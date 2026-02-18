using UnityEngine;
using TowerDefense.Core;
using TowerDefense.Entities;

namespace TowerDefense.Grid
{
    public class TowerSlot : MonoBehaviour
    {
        private HexPiece parentHex;
        private Tower currentTower;
        private GameObject visualIndicator;
        private Vector3 pathFacingDirection;

        public bool IsOccupied => currentTower != null;
        public HexPiece ParentHex => parentHex;
        public Tower CurrentTower => currentTower;
        public Vector3 PathFacingDirection => pathFacingDirection;

        public void Initialize(HexPiece hex, Vector3 facingDirection, bool showIndicator = true)
        {
            parentHex = hex;
            pathFacingDirection = facingDirection;

            // Always add collider to slot for click detection on placed towers
            var slotCollider = gameObject.AddComponent<SphereCollider>();
            slotCollider.radius = 3.0f;

            if (showIndicator)
            {
                CreateVisualIndicator();
            }
        }

        private void CreateVisualIndicator()
        {
            visualIndicator = MaterialCache.CreatePrimitive(PrimitiveType.Cylinder);
            visualIndicator.transform.SetParent(transform);
            visualIndicator.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            visualIndicator.transform.localScale = new Vector3(4.8f, 0.1f, 4.8f);

            // Set color
            var renderer = visualIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = Core.MaterialCache.CreateUnlit(new Color(0.2f, 0.6f, 0.2f, 0.5f));
            }
        }

        public bool PlaceTower(Tower tower)
        {
            if (IsOccupied) return false;

            currentTower = tower;
            tower.transform.position = transform.position;
            tower.transform.SetParent(transform);
            if (visualIndicator != null)
                visualIndicator.SetActive(false);

            return true;
        }

        public Tower RemoveTower()
        {
            if (!IsOccupied) return null;

            Tower tower = currentTower;
            currentTower = null;
            if (visualIndicator != null)
                visualIndicator.SetActive(true);

            return tower;
        }

        public void SetHighlight(bool highlighted)
        {
            if (visualIndicator != null)
            {
                var renderer = visualIndicator.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = highlighted
                        ? new Color(0.2f, 0.9f, 0.2f, 0.8f)
                        : new Color(0.2f, 0.6f, 0.2f, 0.5f);
                }
            }
        }

        public void DestroySelf()
        {
            if (currentTower != null)
            {
                Destroy(currentTower.gameObject);
                currentTower = null;
            }
            if (visualIndicator != null)
            {
                Destroy(visualIndicator);
            }
            Destroy(gameObject);
        }
    }
}
