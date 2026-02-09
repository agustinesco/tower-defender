using System.Collections.Generic;

namespace TowerDefense.Grid
{
    [System.Serializable]
    public class HexPieceData
    {
        public HexCoord Coord;
        public HexPieceType Type;
        public List<int> ConnectedEdges;
        public bool IsSpawnPoint => Type == HexPieceType.DeadEnd;
        public bool IsCastle => Type == HexPieceType.Castle;
        public bool IsGoblinCamp => Type == HexPieceType.GoblinCamp;

        public HexPieceData(HexCoord coord, HexPieceType type, List<int> connectedEdges)
        {
            Coord = coord;
            Type = type;
            ConnectedEdges = connectedEdges;
        }
    }
}