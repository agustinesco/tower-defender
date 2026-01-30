namespace TowerDefense.Grid
{
    public enum HexPieceType
    {
        Castle,     // 1 connection, center piece
        Straight,   // 2 connections, opposite edges
        Bend,       // 2 connections, adjacent edges
        Fork,       // 3 connections, T-junction
        DeadEnd     // 1 connection, spawn point
    }
}