using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Core;

namespace TowerDefense.Grid
{
    public class TerrainManager : MonoBehaviour
    {
        private const float GroundPlaneY = -0.5f;
        private const float TerrainHexY = -0.05f;
        private const float GroundPlaneSize = 500f;
        private const float PerlinScale = 0.15f;

        private static readonly Color BaseColor = new Color(0.18f, 0.22f, 0.12f);
        private static readonly Color GroundColor = new Color(0.12f, 0.10f, 0.08f);

        private Dictionary<HexCoord, GameObject> terrainHexes = new Dictionary<HexCoord, GameObject>();
        private Mesh sharedHexMesh;
        private Material sharedTerrainMaterial;
        private GameObject groundPlane;
        private Material groundPlaneMaterial;

        private static readonly int ColorID = Shader.PropertyToID("_Color");

        public void Initialize()
        {
            sharedHexMesh = HexMeshGenerator.CreateHexMesh();
            sharedTerrainMaterial = MaterialCache.CreateUnlit(BaseColor);

            // Create ground plane
            groundPlane = new GameObject("GroundPlane");
            groundPlane.transform.SetParent(transform);

            var mf = groundPlane.AddComponent<MeshFilter>();
            mf.sharedMesh = MaterialCache.GetPrimitiveMesh(PrimitiveType.Quad);

            var mr = groundPlane.AddComponent<MeshRenderer>();
            groundPlaneMaterial = MaterialCache.CreateUnlit(GroundColor);
            mr.sharedMaterial = groundPlaneMaterial;

            groundPlane.transform.position = new Vector3(0f, GroundPlaneY, 0f);
            groundPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            groundPlane.transform.localScale = new Vector3(GroundPlaneSize, GroundPlaneSize, 1f);
        }

        public void UpdateTerrain(Dictionary<HexCoord, HexPieceData> mapData)
        {
            // Compute desired set: all empty neighbors of placed pieces
            var desiredSet = new HashSet<HexCoord>();
            foreach (var coord in mapData.Keys)
            {
                for (int i = 0; i < 6; i++)
                {
                    HexCoord neighbor = coord.GetNeighbor(i);
                    if (!mapData.ContainsKey(neighbor))
                        desiredSet.Add(neighbor);
                }
            }

            // Remove terrain hexes no longer needed
            var toRemove = new List<HexCoord>();
            foreach (var kvp in terrainHexes)
            {
                if (!desiredSet.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                Destroy(terrainHexes[toRemove[i]]);
                terrainHexes.Remove(toRemove[i]);
            }

            // Add new terrain hexes
            foreach (var coord in desiredSet)
            {
                if (terrainHexes.ContainsKey(coord))
                    continue;

                var obj = new GameObject($"Terrain_{coord.q}_{coord.r}");
                obj.transform.SetParent(transform);

                var mf = obj.AddComponent<MeshFilter>();
                mf.sharedMesh = sharedHexMesh;

                var mr = obj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = sharedTerrainMaterial;

                Vector3 worldPos = HexGrid.HexToWorld(coord);
                obj.transform.position = new Vector3(worldPos.x, TerrainHexY, worldPos.z);

                // Apply per-hex color via MaterialPropertyBlock
                var block = MaterialCache.GetPropertyBlock();
                block.SetColor(ColorID, GetPerlinColor(coord));
                mr.SetPropertyBlock(block);
                MaterialCache.ReturnPropertyBlock(block);

                terrainHexes[coord] = obj;
            }
        }

        private Color GetPerlinColor(HexCoord coord)
        {
            Vector3 world = HexGrid.HexToWorld(coord);
            float noise = Mathf.PerlinNoise(
                world.x * PerlinScale + 1000f,
                world.z * PerlinScale + 1000f
            );

            Color.RGBToHSV(BaseColor, out float h, out float s, out float v);
            h += (noise - 0.5f) * 0.16f;
            s += (noise - 0.5f) * 0.20f;
            v += (noise - 0.5f) * 0.12f;

            h = Mathf.Repeat(h, 1f);
            s = Mathf.Clamp01(s);
            v = Mathf.Clamp01(v);

            return Color.HSVToRGB(h, s, v);
        }

        private void OnDestroy()
        {
            if (sharedHexMesh != null) Destroy(sharedHexMesh);
            if (sharedTerrainMaterial != null) Destroy(sharedTerrainMaterial);
            if (groundPlaneMaterial != null) Destroy(groundPlaneMaterial);

            foreach (var kvp in terrainHexes)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            terrainHexes.Clear();
        }
    }
}
