using UnityEngine;
using System.Collections.Generic;

namespace TowerDefense.Grid
{
    public static class HexMeshGenerator
    {
        public static Mesh CreateHexMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "HexMesh";

            Vector3[] vertices = new Vector3[7]; // Center + 6 corners
            int[] triangles = new int[18]; // 6 triangles * 3 vertices

            vertices[0] = Vector3.zero; // Center

            // Flat-top hex: corners at 0, 60, 120, 180, 240, 300 degrees
            // (edges/flat sides are at 30, 90, 150, 210, 270, 330 degrees)
            for (int i = 0; i < 6; i++)
            {
                float angle = 60f * i; // Corners at 0, 60, 120, 180, 240, 300
                float rad = angle * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(rad) * HexGrid.OuterRadius,
                    0f,
                    Mathf.Sin(rad) * HexGrid.OuterRadius
                );
            }

            // Clockwise winding for upward-facing normals (viewed from Y+)
            for (int i = 0; i < 6; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % 6 + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        // Get the direction vector for a given edge (0-5)
        // Matches HexCoord neighbor directions for flat-top hex layout
        public static Vector3 GetEdgeDirection(int edge)
        {
            // Edge directions matching HexCoord.Directions world positions:
            // Edge 0 (1,0):   30 degrees (right, slightly up)
            // Edge 1 (0,1):   90 degrees (up)
            // Edge 2 (-1,1): 150 degrees (upper-left)
            // Edge 3 (-1,0): 210 degrees (left, slightly down)
            // Edge 4 (0,-1): 270 degrees (down)
            // Edge 5 (1,-1): 330 degrees (lower-right)
            float angle = 60f * edge + 30f;
            float rad = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        }

        public static Mesh CreatePathMesh(List<int> connectedEdges)
        {
            if (connectedEdges == null || connectedEdges.Count == 0)
                return null;

            Mesh mesh = new Mesh();
            mesh.name = "PathMesh";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            const float pathWidth = 2.0f;
            const float halfWidth = pathWidth / 2f;

            // For each edge, create a path from center to edge midpoint
            foreach (int edge in connectedEdges)
            {
                Vector3 direction = GetEdgeDirection(edge);
                Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x);

                Vector3 edgeMidpoint = direction * HexGrid.InnerRadius;

                int startIndex = vertices.Count;

                // Quad from center to edge
                vertices.Add(perpendicular * halfWidth);                    // Center left
                vertices.Add(-perpendicular * halfWidth);                   // Center right
                vertices.Add(edgeMidpoint + perpendicular * halfWidth);     // Edge left
                vertices.Add(edgeMidpoint - perpendicular * halfWidth);     // Edge right

                // Two triangles for the quad - clockwise winding for upward normals
                triangles.Add(startIndex);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 1);

                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 3);
            }

            // Add center circle if multiple paths
            if (connectedEdges.Count > 1)
            {
                int centerStart = vertices.Count;
                int segments = 16;
                vertices.Add(Vector3.zero); // Center point

                for (int i = 0; i < segments; i++)
                {
                    float angle = (360f / segments) * i * Mathf.Deg2Rad;
                    vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * halfWidth * 1.2f);
                }

                // Clockwise winding
                for (int i = 0; i < segments; i++)
                {
                    triangles.Add(centerStart);
                    triangles.Add(centerStart + 1 + (i + 1) % segments);
                    triangles.Add(centerStart + 1 + i);
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}