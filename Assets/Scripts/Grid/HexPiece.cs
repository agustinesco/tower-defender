using UnityEngine;
using System.Collections.Generic;

namespace TowerDefense.Grid
{
    public class HexPiece : MonoBehaviour
    {
        [SerializeField] private MeshFilter hexMeshFilter;
        [SerializeField] private MeshRenderer hexRenderer;
        [SerializeField] private MeshFilter pathMeshFilter;
        [SerializeField] private MeshRenderer pathRenderer;

        private HexPieceData data;
        private List<TowerSlot> slots = new List<TowerSlot>();

        public HexPieceData Data => data;
        public List<TowerSlot> Slots => slots;

        public void Initialize(HexPieceData pieceData, Material hexMaterial, Material pathMaterial, Material castleMaterial)
        {
            data = pieceData;
            transform.position = HexGrid.HexToWorld(data.Coord);
            gameObject.name = $"HexPiece_{data.Coord}_{data.Type}";

            // Create default materials if not provided
            if (hexMaterial == null)
            {
                hexMaterial = new Material(Shader.Find("Unlit/Color"));
                hexMaterial.color = new Color(0.75f, 0.75f, 0.75f); // Light gray
            }
            if (pathMaterial == null)
            {
                pathMaterial = new Material(Shader.Find("Unlit/Color"));
                pathMaterial.color = new Color(0.35f, 0.25f, 0.15f); // Dark brown path color
            }
            if (castleMaterial == null)
            {
                castleMaterial = new Material(Shader.Find("Unlit/Color"));
                castleMaterial.color = new Color(0.9f, 0.75f, 0.2f); // Gold
            }

            // Create hex base if not assigned
            if (hexMeshFilter == null)
            {
                GameObject hexBase = new GameObject("HexBase");
                hexBase.transform.SetParent(transform);
                hexBase.transform.localPosition = Vector3.zero;
                hexMeshFilter = hexBase.AddComponent<MeshFilter>();
                hexRenderer = hexBase.AddComponent<MeshRenderer>();
            }

            hexMeshFilter.mesh = HexMeshGenerator.CreateHexMesh();
            hexRenderer.material = data.IsCastle ? castleMaterial : hexMaterial;

            // Create path if needed
            if (data.ConnectedEdges.Count > 0)
            {
                if (pathMeshFilter == null)
                {
                    GameObject pathObj = new GameObject("Path");
                    pathObj.transform.SetParent(transform);
                    pathObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    pathMeshFilter = pathObj.AddComponent<MeshFilter>();
                    pathRenderer = pathObj.AddComponent<MeshRenderer>();
                }

                pathMeshFilter.mesh = HexMeshGenerator.CreatePathMesh(data.ConnectedEdges);
                pathRenderer.material = pathMaterial;
            }

            // Create castle visual if castle type
            if (data.IsCastle)
            {
                CreateCastleVisual();
            }

            // Generate tower slots (skip for castle)
            if (!data.IsCastle)
            {
                GenerateSlots();
            }
        }

        private void CreateCastleVisual()
        {
            GameObject castle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            castle.name = "CastleModel";
            castle.transform.SetParent(transform);
            castle.transform.localPosition = new Vector3(0f, 1f, 0f);
            castle.transform.localScale = new Vector3(2f, 2f, 2f);

            var collider = castle.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private void GenerateSlots()
        {
            const float slotDistance = 1.2f;
            const float slotOffsetAlongPath = 2.5f;

            foreach (int edge in data.ConnectedEdges)
            {
                Vector3 direction = HexMeshGenerator.GetEdgeDirection(edge);
                Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x);

                Vector3 pathPoint = direction * slotOffsetAlongPath;

                CreateSlot(transform.position + pathPoint + perpendicular * slotDistance);
                CreateSlot(transform.position + pathPoint - perpendicular * slotDistance);
            }
        }

        private void CreateSlot(Vector3 position)
        {
            GameObject slotObj = new GameObject("TowerSlot");
            slotObj.transform.SetParent(transform);
            slotObj.transform.position = position;

            TowerSlot slot = slotObj.AddComponent<TowerSlot>();
            slot.Initialize(this);
            slots.Add(slot);
        }

        public Vector3 GetEdgeWorldPosition(int edge)
        {
            Vector3 direction = HexMeshGenerator.GetEdgeDirection(edge);
            return transform.position + direction * HexGrid.InnerRadius;
        }
    }
}