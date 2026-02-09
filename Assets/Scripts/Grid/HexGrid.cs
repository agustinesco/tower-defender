using UnityEngine;

namespace TowerDefense.Grid
{
    public static class HexGrid
    {
        public const float OuterRadius = 20f;
        public static readonly float InnerRadius = OuterRadius * Mathf.Sqrt(3f) / 2f;

        // Flat-top hex layout
        public static Vector3 HexToWorld(HexCoord coord)
        {
            float x = OuterRadius * 1.5f * coord.q;
            float z = InnerRadius * 2f * (coord.r + coord.q * 0.5f);
            return new Vector3(x, 0f, z);
        }

        public static HexCoord WorldToHex(Vector3 worldPos)
        {
            float q = worldPos.x / (OuterRadius * 1.5f);
            float r = (worldPos.z / (InnerRadius * 2f)) - q * 0.5f;
            float s = -q - r;
            int rq = Mathf.RoundToInt(q), rr = Mathf.RoundToInt(r), rs = Mathf.RoundToInt(s);
            float qd = Mathf.Abs(rq - q), rd = Mathf.Abs(rr - r), sd = Mathf.Abs(rs - s);
            if (qd > rd && qd > sd) rq = -rr - rs;
            else if (rd > sd) rr = -rq - rs;
            return new HexCoord(rq, rr);
        }

        public static Vector3 GetEdgeMidpoint(HexCoord coord, int edge)
        {
            Vector3 center = HexToWorld(coord);
            Vector3 direction = HexMeshGenerator.GetEdgeDirection(edge);
            return center + direction * InnerRadius;
        }

        public static Vector3[] GetHexCorners(HexCoord coord)
        {
            Vector3 center = HexToWorld(coord);
            Vector3[] corners = new Vector3[6];
            // Flat-top hex: corners at 0, 60, 120, 180, 240, 300 degrees
            for (int i = 0; i < 6; i++)
            {
                float angle = 60f * i;
                float rad = angle * Mathf.Deg2Rad;
                corners[i] = center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * OuterRadius;
            }
            return corners;
        }
    }
}
