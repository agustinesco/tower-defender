namespace TowerDefense.Grid
{
    public enum RotationPattern
    {
        Straight,   // 1 variant: entry + opposite
        Bend,       // 2 variants: entry + one edge adjacent to opposite
        Fork,       // 10 variants: entry + any 2 of remaining 5 edges
        DeadEnd,    // 1 variant: entry only
        Cross,      // 2 variants: entry + opposite + 2 more edges
        Star,       // 2 variants: entry + opposite + 3 more edges (one excluded)
        Crossroads  // 1 variant: all 6 edges
    }
}
