namespace TowerDefense.Grid
{
    public enum HexPieceType
    {
        Castle,     // 1 connection, center piece
        Straight,   // 2 connections, opposite edges
        Simple,     // 2 connections, opposite edges (starter piece)
        Fork,       // 3 connections, T-junction
        DeadEnd,    // 1 connection, spawn point
        GoblinCamp, // 2 connections (straight), no tower slots, spawns enemies
        Cross,      // 4 connections, crossroad
        Star,       // 5 connections
        Crossroads  // 6 connections, all edges
    }
}
