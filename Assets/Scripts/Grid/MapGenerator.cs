using System.Collections.Generic;
using UnityEngine;
using TowerDefense.Data;

namespace TowerDefense.Grid
{
    public struct OrePatch
    {
        public HexCoord Coord;
        public ResourceType ResourceType;
        public int BaseYield;

        public OrePatch(HexCoord coord, ResourceType type, int baseYield)
        {
            Coord = coord;
            ResourceType = type;
            BaseYield = baseYield;
        }
    }

    public class MapGenerator
    {
        private Dictionary<HexCoord, HexPieceData> pieces = new Dictionary<HexCoord, HexPieceData>();
        private System.Random random;
        private HashSet<HexCoord> hiddenSpawners = new HashSet<HexCoord>();
        private Dictionary<HexCoord, OrePatch> orePatches = new Dictionary<HexCoord, OrePatch>();

        public HexCoord? GuaranteedOreCoord { get; private set; }

        public MapGenerator(int seed = -1)
        {
            random = seed >= 0 ? new System.Random(seed) : new System.Random();
        }

        public Dictionary<HexCoord, HexPieceData> GenerateInitialCastle()
        {
            pieces.Clear();

            int castleExitEdge = random.Next(6);
            var castle = new HexPieceData(
                new HexCoord(0, 0),
                HexPieceType.Castle,
                new List<int> { castleExitEdge }
            );
            pieces[castle.Coord] = castle;

            return new Dictionary<HexCoord, HexPieceData>(pieces);
        }

        public List<HexPieceData> GenerateStartingPath(int count = 5)
        {
            var generated = new List<HexPieceData>();

            for (int i = 0; i < count; i++)
            {
                // Find all open edges
                var openEdges = new List<(HexCoord coord, int edge)>();
                foreach (var kvp in pieces)
                {
                    foreach (int edge in kvp.Value.ConnectedEdges)
                    {
                        HexCoord neighbor = kvp.Value.Coord.GetNeighbor(edge);
                        if (!pieces.ContainsKey(neighbor))
                            openEdges.Add((kvp.Value.Coord, edge));
                    }
                }

                if (openEdges.Count == 0) break;

                // Pick a random open edge
                var chosen = openEdges[random.Next(openEdges.Count)];
                HexCoord newCoord = chosen.coord.GetNeighbor(chosen.edge);
                int entryEdge = HexCoord.OppositeEdge(chosen.edge);

                // Pick piece shape: 60% straight, 25% bend, 15% fork
                float roll = (float)random.NextDouble();
                List<int> edges;

                if (roll < 0.60f)
                {
                    // Straight: entry + opposite
                    edges = new List<int> { entryEdge, HexCoord.OppositeEdge(entryEdge) };
                }
                else if (roll < 0.85f)
                {
                    // Bend: entry + one adjacent to opposite
                    int opposite = HexCoord.OppositeEdge(entryEdge);
                    int offset = random.Next(2) == 0 ? 1 : -1;
                    int bendExit = ((opposite + offset) % 6 + 6) % 6;
                    edges = new List<int> { entryEdge, bendExit };
                }
                else
                {
                    // Fork: entry + opposite + one side
                    int opposite = HexCoord.OppositeEdge(entryEdge);
                    int offset = random.Next(2) == 0 ? 1 : -1;
                    int forkSide = ((opposite + offset) % 6 + 6) % 6;
                    edges = new List<int> { entryEdge, opposite, forkSide };
                }

                // Validate: no exit edge should point to an existing piece that doesn't connect back
                bool valid = true;
                foreach (int edge in edges)
                {
                    if (edge == entryEdge) continue;
                    HexCoord neighbor = newCoord.GetNeighbor(edge);
                    if (pieces.ContainsKey(neighbor))
                    {
                        int requiredEdge = HexCoord.OppositeEdge(edge);
                        if (!pieces[neighbor].ConnectedEdges.Contains(requiredEdge))
                        {
                            valid = false;
                            break;
                        }
                    }
                }

                if (!valid)
                {
                    // Fallback to straight
                    edges = new List<int> { entryEdge, HexCoord.OppositeEdge(entryEdge) };
                }

                // Determine piece type from edge count
                HexPieceType type;
                if (edges.Count == 3) type = HexPieceType.Fork;
                else if (edges.Count == 2 && edges[1] == HexCoord.OppositeEdge(edges[0])) type = HexPieceType.Straight;
                else type = HexPieceType.Bend;

                var pieceData = new HexPieceData(newCoord, type, edges);
                pieces[newCoord] = pieceData;
                generated.Add(pieceData);
            }

            return generated;
        }

        public HexPieceData PlacePiece(HexCoord coord, HexPieceType type, List<int> connectedEdges)
        {
            var pieceData = new HexPieceData(coord, type, connectedEdges);
            pieces[coord] = pieceData;
            return pieceData;
        }

        public HashSet<HexCoord> GenerateHiddenSpawners(int count = 6, int minDistance = 3, int maxDistance = 7)
        {
            hiddenSpawners.Clear();
            var origin = new HexCoord(0, 0);
            var candidates = new List<HexCoord>();

            // Collect all coords in the distance range
            for (int q = -maxDistance; q <= maxDistance; q++)
            {
                for (int r = -maxDistance; r <= maxDistance; r++)
                {
                    var coord = new HexCoord(q, r);
                    int dist = origin.DistanceTo(coord);
                    if (dist >= minDistance && dist <= maxDistance && !pieces.ContainsKey(coord))
                    {
                        candidates.Add(coord);
                    }
                }
            }

            // Shuffle and pick up to count, ensuring some spread
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = temp;
            }

            foreach (var coord in candidates)
            {
                if (hiddenSpawners.Count >= count) break;

                // Ensure spawners aren't too close to each other (min 2 hexes apart)
                bool tooClose = false;
                foreach (var existing in hiddenSpawners)
                {
                    if (coord.DistanceTo(existing) < 2)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    hiddenSpawners.Add(coord);
            }

            return new HashSet<HexCoord>(hiddenSpawners);
        }

        public bool IsHiddenSpawner(HexCoord coord)
        {
            return hiddenSpawners.Contains(coord);
        }

        public void RemoveHiddenSpawner(HexCoord coord)
        {
            hiddenSpawners.Remove(coord);
        }

        public HashSet<HexCoord> GetHiddenSpawners()
        {
            return new HashSet<HexCoord>(hiddenSpawners);
        }

        public List<HexCoord> GetSpawnPoints()
        {
            var spawnPoints = new List<HexCoord>();
            foreach (var piece in pieces.Values)
            {
                if (piece.IsSpawnPoint)
                {
                    spawnPoints.Add(piece.Coord);
                }
            }
            return spawnPoints;
        }

        public Dictionary<HexCoord, OrePatch> GenerateOrePatches(int minDistance = 2, int maxDistance = 6, int zoneBoundary = 3)
        {
            orePatches.Clear();
            var origin = new HexCoord(0, 0);
            var innerCandidates = new List<HexCoord>();
            var outerCandidates = new List<HexCoord>();

            for (int q = -maxDistance; q <= maxDistance; q++)
            {
                for (int r = -maxDistance; r <= maxDistance; r++)
                {
                    var coord = new HexCoord(q, r);
                    int dist = origin.DistanceTo(coord);
                    if (dist >= minDistance && dist <= maxDistance
                        && !pieces.ContainsKey(coord)
                        && !hiddenSpawners.Contains(coord))
                    {
                        if (dist <= zoneBoundary)
                            innerCandidates.Add(coord);
                        else
                            outerCandidates.Add(coord);
                    }
                }
            }

            // Guarantee one IronOre adjacent to starting tiles
            int guaranteedIron = PlaceGuaranteedIronNode(innerCandidates);

            // Shuffle both lists
            Shuffle(innerCandidates);
            Shuffle(outerCandidates);

            // Zone 1 (inner): IronOre and Gems only, 6 nodes (minus guaranteed)
            ResourceType[] innerTypes = { ResourceType.IronOre, ResourceType.Gems };
            int innerCount = 6 - guaranteedIron;
            PlaceOrePatches(innerCandidates, innerTypes, innerCount, origin, minDistance, zoneBoundary);

            // Zone 2+ (outer): Florpus and Adamantite only, 4 nodes
            ResourceType[] outerTypes = { ResourceType.Florpus, ResourceType.Adamantite };
            int outerCount = 4;
            PlaceOrePatches(outerCandidates, outerTypes, outerCount, origin, zoneBoundary + 1, maxDistance);

            return new Dictionary<HexCoord, OrePatch>(orePatches);
        }

        private int PlaceGuaranteedIronNode(List<HexCoord> innerCandidates)
        {
            // Find empty hexes reachable from an open edge of the existing path.
            // An open edge is a connected edge that points to an empty hex.
            var reachable = new List<HexCoord>();
            foreach (var kvp in pieces)
            {
                foreach (int edge in kvp.Value.ConnectedEdges)
                {
                    HexCoord neighbor = kvp.Value.Coord.GetNeighbor(edge);
                    if (!pieces.ContainsKey(neighbor) && innerCandidates.Contains(neighbor))
                    {
                        if (!reachable.Contains(neighbor))
                            reachable.Add(neighbor);
                    }
                }
            }

            if (reachable.Count == 0) return 0;

            var chosen = reachable[random.Next(reachable.Count)];
            orePatches[chosen] = new OrePatch(chosen, ResourceType.IronOre, 1);
            innerCandidates.Remove(chosen);
            GuaranteedOreCoord = chosen;
            return 1;
        }

        private void PlaceOrePatches(List<HexCoord> candidates, ResourceType[] types, int count, HexCoord origin, int minDist, int maxDist)
        {
            int placed = 0;
            foreach (var coord in candidates)
            {
                if (placed >= count) break;

                bool tooClose = false;
                foreach (var existing in orePatches.Keys)
                {
                    if (coord.DistanceTo(existing) < 2)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                int dist = origin.DistanceTo(coord);
                int range = maxDist - minDist;
                int typeIndex = range > 0
                    ? (dist - minDist) * (types.Length - 1) / range
                    : 0;
                typeIndex = Mathf.Clamp(typeIndex, 0, types.Length - 1);
                var resType = types[typeIndex];
                int baseYield = 1 + (dist - minDist);

                orePatches[coord] = new OrePatch(coord, resType, baseYield);
                placed++;
            }
        }

        private void Shuffle(List<HexCoord> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        public Dictionary<HexCoord, OrePatch> GetOrePatches()
        {
            return new Dictionary<HexCoord, OrePatch>(orePatches);
        }

        public bool IsOrePatch(HexCoord coord)
        {
            return orePatches.ContainsKey(coord);
        }

        public OrePatch? GetOrePatch(HexCoord coord)
        {
            if (orePatches.TryGetValue(coord, out var patch))
                return patch;
            return null;
        }
    }
}
