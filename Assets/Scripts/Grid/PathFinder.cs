using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Grid
{
    public class PathFinder
    {
        private Dictionary<HexCoord, HexPieceData> map;

        public PathFinder(Dictionary<HexCoord, HexPieceData> mapData)
        {
            map = mapData;
        }

        public List<Vector3> FindPathToCastle(HexCoord start)
        {
            Debug.Log($"PathFinder: Finding path from {start}");

            if (!map.ContainsKey(start))
            {
                Debug.LogError($"PathFinder: Start position {start} not in map!");
                return new List<Vector3>();
            }

            var startPiece = map[start];
            Debug.Log($"PathFinder: Start piece type: {startPiece.Type}, edges: [{string.Join(", ", startPiece.ConnectedEdges)}]");

            var hexPath = BFSToCastle(start);
            if (hexPath == null || hexPath.Count == 0)
            {
                Debug.LogWarning($"PathFinder: No path found from {start} to castle");
                return new List<Vector3>();
            }

            Debug.Log($"PathFinder: Found hex path with {hexPath.Count} hexes");
            return ConvertToWaypoints(hexPath);
        }

        private List<HexCoord> BFSToCastle(HexCoord start)
        {
            var queue = new Queue<HexCoord>();
            var visited = new HashSet<HexCoord>();
            var cameFrom = new Dictionary<HexCoord, HexCoord>();

            queue.Enqueue(start);
            visited.Add(start);

            HexCoord? castleCoord = null;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (map.TryGetValue(current, out var piece) && piece.IsCastle)
                {
                    castleCoord = current;
                    break;
                }

                if (!map.TryGetValue(current, out var currentPiece))
                    continue;

                foreach (int edge in currentPiece.ConnectedEdges)
                {
                    var neighbor = current.GetNeighbor(edge);
                    if (visited.Contains(neighbor))
                        continue;

                    if (!map.TryGetValue(neighbor, out var neighborPiece))
                        continue;

                    int requiredEdge = HexCoord.OppositeEdge(edge);
                    if (!neighborPiece.ConnectedEdges.Contains(requiredEdge))
                        continue;

                    visited.Add(neighbor);
                    cameFrom[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }

            if (!castleCoord.HasValue)
                return null;

            var path = new List<HexCoord>();
            var node = castleCoord.Value;
            while (cameFrom.ContainsKey(node))
            {
                path.Add(node);
                node = cameFrom[node];
            }
            path.Add(start);
            path.Reverse();

            return path;
        }

        private List<Vector3> ConvertToWaypoints(List<HexCoord> hexPath)
        {
            var waypoints = new List<Vector3>();

            for (int i = 0; i < hexPath.Count; i++)
            {
                var coord = hexPath[i];
                var worldPos = HexGrid.HexToWorld(coord);

                if (i == 0)
                {
                    // Start at spawn point center
                    waypoints.Add(worldPos);
                }
                else
                {
                    // Add entry point, then center
                    var prevCoord = hexPath[i - 1];
                    int entryEdge = GetConnectingEdge(prevCoord, coord);
                    if (entryEdge >= 0)
                    {
                        waypoints.Add(GetEdgeMidpoint(coord, entryEdge));
                    }
                    waypoints.Add(worldPos);
                }
            }

            return waypoints;
        }

        private int GetConnectingEdge(HexCoord from, HexCoord to)
        {
            for (int i = 0; i < 6; i++)
            {
                if (from.GetNeighbor(i) == to)
                    return HexCoord.OppositeEdge(i);
            }
            return -1;
        }

        private Vector3 GetEdgeMidpoint(HexCoord coord, int edge)
        {
            Vector3 center = HexGrid.HexToWorld(coord);
            Vector3 direction = HexMeshGenerator.GetEdgeDirection(edge);
            return center + direction * HexGrid.InnerRadius;
        }
    }
}
