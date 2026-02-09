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
            return FindPathToTarget(start, coord => map.TryGetValue(coord, out var p) && p.IsCastle);
        }

        public List<Vector3> FindPathToCoord(HexCoord start, HexCoord target)
        {
            return FindPathToTarget(start, coord => coord == target);
        }

        private List<Vector3> FindPathToTarget(HexCoord start, System.Func<HexCoord, bool> isTarget)
        {
            if (!map.ContainsKey(start))
                return new List<Vector3>();

            var hexPath = BFS(start, isTarget);
            if (hexPath == null || hexPath.Count == 0)
                return new List<Vector3>();

            return ConvertToWaypoints(hexPath);
        }

        private List<HexCoord> BFS(HexCoord start, System.Func<HexCoord, bool> isTarget)
        {
            var queue = new Queue<HexCoord>();
            var visited = new HashSet<HexCoord>();
            var cameFrom = new Dictionary<HexCoord, HexCoord>();

            queue.Enqueue(start);
            visited.Add(start);

            HexCoord? targetCoord = null;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (isTarget(current))
                {
                    targetCoord = current;
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

            if (!targetCoord.HasValue)
                return null;

            var path = new List<HexCoord>();
            var node = targetCoord.Value;
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
