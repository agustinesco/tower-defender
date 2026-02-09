namespace TowerDefense.Grid
{
    public enum RotationPattern
    {
        Straight,   // 1 variant: entry + opposite
        Bend,       // 2 variants: entry + one edge adjacent to opposite
        Fork,       // 2 variants: entry + opposite + one edge adjacent to opposite
        DeadEnd,    // 1 variant: entry only
        Cross,      // 2 variants: entry + opposite + 2 more edges
        Star,       // 2 variants: entry + opposite + 3 more edges (one excluded)
        Crossroads  // 1 variant: all 6 edges
    }
}
