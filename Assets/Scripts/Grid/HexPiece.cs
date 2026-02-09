using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Data;

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
        private bool _isGhost;
        private Material ghostHexMat;
        private Material ghostPathMat;
        private SphereCollider ghostCollider;

        public HexPieceData Data => data;
        public List<TowerSlot> Slots => slots;
        public bool IsGhost => _isGhost;

        public void Initialize(HexPieceData pieceData, HexPieceConfig config, Material hexMaterial, Material pathMaterial, Material castleMaterial)
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

            // Create goblin camp visual
            if (data.IsGoblinCamp)
            {
                CreateGoblinCampVisual();
            }

            // Generate pre-placed tower slots alongside paths
            if (config != null ? config.allowsTowerSlots : (!data.IsCastle && !data.IsGoblinCamp))
            {
                GenerateTowerSlots();
            }
        }

        private void CreateCastleVisual()
        {
            GameObject castle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            castle.name = "CastleModel";
            castle.transform.SetParent(transform);
            castle.transform.localPosition = new Vector3(0f, 2f, 0f);
            castle.transform.localScale = new Vector3(4f, 4f, 4f);

            var collider = castle.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private void CreateGoblinCampVisual()
        {
            // Tent body
            GameObject tent = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            tent.name = "GoblinTent";
            tent.transform.SetParent(transform);
            tent.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            tent.transform.localScale = new Vector3(5f, 2.4f, 5f);

            var tentCollider = tent.GetComponent<Collider>();
            if (tentCollider != null) Destroy(tentCollider);

            var tentRenderer = tent.GetComponent<Renderer>();
            if (tentRenderer != null)
            {
                tentRenderer.material = new Material(Shader.Find("Unlit/Color"));
                tentRenderer.material.color = new Color(0.3f, 0.5f, 0.15f); // Dark olive green
            }

            // Flag pole
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "FlagPole";
            pole.transform.SetParent(transform);
            pole.transform.localPosition = new Vector3(2.4f, 3.6f, 0f);
            pole.transform.localScale = new Vector3(0.3f, 2.4f, 0.3f);

            var poleCollider = pole.GetComponent<Collider>();
            if (poleCollider != null) Destroy(poleCollider);

            var poleRenderer = pole.GetComponent<Renderer>();
            if (poleRenderer != null)
            {
                poleRenderer.material = new Material(Shader.Find("Unlit/Color"));
                poleRenderer.material.color = new Color(0.4f, 0.25f, 0.1f); // Brown
            }

            // Flag
            GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = "Flag";
            flag.transform.SetParent(transform);
            flag.transform.localPosition = new Vector3(2.4f, 5.6f, 0.8f);
            flag.transform.localScale = new Vector3(0.1f, 1.2f, 1.6f);

            var flagCollider = flag.GetComponent<Collider>();
            if (flagCollider != null) Destroy(flagCollider);

            var flagRenderer = flag.GetComponent<Renderer>();
            if (flagRenderer != null)
            {
                flagRenderer.material = new Material(Shader.Find("Unlit/Color"));
                flagRenderer.material.color = new Color(0.7f, 0.15f, 0.1f); // Red flag
            }
        }

        public Vector3 GetEdgeWorldPosition(int edge)
        {
            Vector3 direction = HexMeshGenerator.GetEdgeDirection(edge);
            return transform.position + direction * HexGrid.InnerRadius;
        }

        private void GenerateTowerSlots()
        {
            const float minT = 0.3f;
            const float maxT = 0.8f;
            const float perpendicularOffset = 6.5f;
            const float minSlotDistance = 3f;
            const float pathHalfWidth = 4.0f;
            const float pathClearance = pathHalfWidth + 1.5f;
            const int maxSlotsPerPiece = 2;

            List<Vector3> placedPositions = new List<Vector3>();
            var candidateSlots = new List<(Vector3 localPos, Vector3 facingDir, int edge, int side)>();

            foreach (int edge in data.ConnectedEdges)
            {
                Vector3 dir = HexMeshGenerator.GetEdgeDirection(edge);
                Vector3 edgeMidpoint = dir * HexGrid.InnerRadius;
                Vector3 perpendicular = new Vector3(-dir.z, 0f, dir.x);

                // Try placing a slot on each side of the path
                for (int side = 0; side < 2; side++)
                {
                    float t = Random.Range(minT, maxT);
                    Vector3 pathPoint = edgeMidpoint * t;

                    float sign = (side == 0) ? 1f : -1f;
                    Vector3 localPos = pathPoint + perpendicular * (sign * perpendicularOffset);

                    if (!IsSlotPositionValid(localPos, placedPositions, minSlotDistance, pathClearance))
                        continue;

                    placedPositions.Add(localPos);

                    Vector3 facingDir = (pathPoint - localPos);
                    facingDir.y = 0f;
                    facingDir = facingDir.normalized;

                    candidateSlots.Add((localPos, facingDir, edge, side));
                }
            }

            // Shuffle candidates randomly
            for (int i = candidateSlots.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = candidateSlots[i];
                candidateSlots[i] = candidateSlots[j];
                candidateSlots[j] = temp;
            }

            // Keep only the first maxSlotsPerPiece
            int slotCount = Mathf.Min(maxSlotsPerPiece, candidateSlots.Count);
            for (int i = 0; i < slotCount; i++)
            {
                var candidate = candidateSlots[i];
                GameObject slotObj = new GameObject($"TowerSlot_Edge{candidate.edge}_Side{candidate.side}");
                slotObj.transform.SetParent(transform);
                slotObj.transform.localPosition = candidate.localPos;

                TowerSlot slot = slotObj.AddComponent<TowerSlot>();
                slot.Initialize(this, candidate.facingDir, true);
                slots.Add(slot);
            }
        }

        private bool IsSlotPositionValid(Vector3 localPos, List<Vector3> placed, float minSlotDist, float pathClearance)
        {
            // Check minimum distance from other slots
            foreach (var existing in placed)
            {
                if (Vector3.Distance(localPos, existing) < minSlotDist)
                    return false;
            }

            // Check distance from ALL path segments (center to edge midpoint)
            foreach (int otherEdge in data.ConnectedEdges)
            {
                Vector3 otherDir = HexMeshGenerator.GetEdgeDirection(otherEdge);
                Vector3 otherEnd = otherDir * HexGrid.InnerRadius;
                float dist = PointToSegmentDistance(localPos, Vector3.zero, otherEnd);
                if (dist < pathClearance)
                    return false;
            }

            return true;
        }

        private static float PointToSegmentDistance(Vector3 point, Vector3 segA, Vector3 segB)
        {
            Vector3 ab = segB - segA;
            ab.y = 0f;
            Vector3 ap = point - segA;
            ap.y = 0f;

            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 0.0001f)
                return ap.magnitude;

            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / abLenSq);
            Vector3 closest = segA + ab * t;
            Vector3 diff = point - closest;
            diff.y = 0f;
            return diff.magnitude;
        }

        public void InitializeAsGhost(HexPieceData pieceData, Material ghostHexMaterial, Material ghostPathMaterial)
        {
            _isGhost = true;
            data = pieceData;
            ghostHexMat = ghostHexMaterial;
            ghostPathMat = ghostPathMaterial;

            transform.position = HexGrid.HexToWorld(data.Coord);
            gameObject.name = $"Ghost_{data.Coord}_{data.Type}";
            gameObject.layer = 0;

            // Create hex base with ghost material
            GameObject hexBase = new GameObject("HexBase");
            hexBase.transform.SetParent(transform);
            hexBase.transform.localPosition = Vector3.zero;
            hexMeshFilter = hexBase.AddComponent<MeshFilter>();
            hexRenderer = hexBase.AddComponent<MeshRenderer>();

            hexMeshFilter.mesh = HexMeshGenerator.CreateHexMesh();
            hexRenderer.material = ghostHexMat;

            // Create path with ghost material
            if (data.ConnectedEdges.Count > 0)
            {
                GameObject pathObj = new GameObject("Path");
                pathObj.transform.SetParent(transform);
                pathObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                pathMeshFilter = pathObj.AddComponent<MeshFilter>();
                pathRenderer = pathObj.AddComponent<MeshRenderer>();

                pathMeshFilter.mesh = HexMeshGenerator.CreatePathMesh(data.ConnectedEdges);
                pathRenderer.material = ghostPathMat;
            }

            // Add sphere collider for tap/click detection
            ghostCollider = gameObject.AddComponent<SphereCollider>();
            ghostCollider.radius = HexGrid.InnerRadius * 0.9f;
            ghostCollider.center = Vector3.zero;
        }

        public void SetGhostHighlight(bool highlighted)
        {
            if (!_isGhost) return;

            float alpha = highlighted ? 0.6f : 0.3f;

            if (hexRenderer != null)
            {
                Color c = hexRenderer.material.color;
                c.a = alpha;
                hexRenderer.material.color = c;
            }

            if (pathRenderer != null)
            {
                Color c = pathRenderer.material.color;
                c.a = alpha;
                pathRenderer.material.color = c;
            }
        }

        public void SetGhostRotation(List<int> newConnectedEdges)
        {
            if (!_isGhost) return;

            data = new HexPieceData(data.Coord, data.Type, newConnectedEdges);

            if (newConnectedEdges.Count > 0)
            {
                if (pathMeshFilter == null)
                {
                    GameObject pathObj = new GameObject("Path");
                    pathObj.transform.SetParent(transform);
                    pathObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    pathMeshFilter = pathObj.AddComponent<MeshFilter>();
                    pathRenderer = pathObj.AddComponent<MeshRenderer>();
                    pathRenderer.material = ghostPathMat;
                }

                pathMeshFilter.mesh = HexMeshGenerator.CreatePathMesh(newConnectedEdges);
            }
        }
    }
}