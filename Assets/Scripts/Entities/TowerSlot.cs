using UnityEngine;
using TowerDefense.Entities;

namespace TowerDefense.Grid
{
    public class TowerSlot : MonoBehaviour
    {
        private HexPiece parentHex;
        private Tower currentTower;
        private GameObject visualIndicator;

        public bool IsOccupied => currentTower != null;
        public HexPiece ParentHex => parentHex;
        public Tower CurrentTower => currentTower;

        public void Initialize(HexPiece hex)
        {
            parentHex = hex;
            CreateVisualIndicator();
        }

        private void CreateVisualIndicator()
        {
            visualIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualIndicator.transform.SetParent(transform);
            visualIndicator.transform.localPosition = Vector3.zero;
            visualIndicator.transform.localScale = new Vector3(1f, 0.1f, 1f);

            // Remove collider from primitive
            var collider = visualIndicator.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            // Set color
            var renderer = visualIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Unlit/Color"));
                renderer.material.color = new Color(0.2f, 0.6f, 0.2f, 0.5f);
            }

            // Add collider to slot for click detection
            var slotCollider = gameObject.AddComponent<SphereCollider>();
            slotCollider.radius = 0.6f;
        }

        public bool PlaceTower(Tower tower)
        {
            if (IsOccupied) return false;

            currentTower = tower;
            tower.transform.position = transform.position;
            tower.transform.SetParent(transform);
            visualIndicator.SetActive(false);

            return true;
        }

        public Tower RemoveTower()
        {
            if (!IsOccupied) return null;

            Tower tower = currentTower;
            currentTower = null;
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
    }
}
