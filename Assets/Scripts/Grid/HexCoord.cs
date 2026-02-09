using UnityEngine;

namespace TowerDefense.Grid
{
    [System.Serializable]
    public struct HexCoord : System.IEquatable<HexCoord>
    {
        public int q;
        public int r;

        public int S => -q - r;

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        // Flat-top hex: 6 directions starting from right (edge 0), counter-clockwise
        public static readonly HexCoord[] Directions = new HexCoord[]
        {
            new HexCoord(1, 0),   // Edge 0: Right
            new HexCoord(0, 1),   // Edge 1: Upper-right
            new HexCoord(-1, 1),  // Edge 2: Upper-left
            new HexCoord(-1, 0),  // Edge 3: Left
            new HexCoord(0, -1),  // Edge 4: Lower-left
            new HexCoord(1, -1),  // Edge 5: Lower-right
        };

        public HexCoord GetNeighbor(int edge)
        {
            edge = ((edge % 6) + 6) % 6;
            return this + Directions[edge];
        }

        public static int OppositeEdge(int edge)
        {
            return (edge + 3) % 6;
        }

        public int DistanceTo(HexCoord other)
        {
            int dq = Mathf.Abs(q - other.q);
            int dr = Mathf.Abs(r - other.r);
            int ds = Mathf.Abs(S - other.S);
            return Mathf.Max(dq, Mathf.Max(dr, ds));
        }

        public static HexCoord operator +(HexCoord a, HexCoord b)
        {
            return new HexCoord(a.q + b.q, a.r + b.r);
        }

        public static HexCoord operator -(HexCoord a, HexCoord b)
        {
            return new HexCoord(a.q - b.q, a.r - b.r);
        }

        public static bool operator ==(HexCoord a, HexCoord b)
        {
            return a.q == b.q && a.r == b.r;
        }

        public static bool operator !=(HexCoord a, HexCoord b)
        {
            return !(a == b);
        }

        public bool Equals(HexCoord other)
        {
            return q == other.q && r == other.r;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            return q * 1000 + r;
        }

        public override string ToString()
        {
            return $"Hex({q}, {r})";
        }
    }
}