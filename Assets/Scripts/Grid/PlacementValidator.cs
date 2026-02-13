using System.Collections.Generic;
using TowerDefense.Data;

namespace TowerDefense.Grid
{
    public class PlacementOption
    {
        public HexCoord Coord;
        public List<PlacementRotation> Rotations;

        public PlacementOption(HexCoord coord)
        {
            Coord = coord;
            Rotations = new List<PlacementRotation>();
        }
    }

    public class PlacementRotation
    {
        public int EntryEdge;
        public List<int> ConnectedEdges;

        public PlacementRotation(int entryEdge, List<int> connectedEdges)
        {
            EntryEdge = entryEdge;
            ConnectedEdges = connectedEdges;
        }
    }

    public class PlacementValidator
    {
        private Dictionary<HexCoord, HexPieceData> map;
        private Dictionary<HexPieceType, HexPieceConfig> pieceConfigs;
        private HashSet<HexCoord> nonReplaceableCoords = new HashSet<HexCoord>();
        private int maxPlacementDistance = -1; // -1 = no limit
        private static readonly HexCoord origin = new HexCoord(0, 0);

        public PlacementValidator(Dictionary<HexCoord, HexPieceData> mapData, Dictionary<HexPieceType, HexPieceConfig> configs)
        {
            map = mapData;
            pieceConfigs = configs;
        }

        public void UpdateMap(Dictionary<HexCoord, HexPieceData> mapData)
        {
            map = mapData;
        }

        public void SetNonReplaceableCoords(HashSet<HexCoord> coords)
        {
            nonReplaceableCoords = coords ?? new HashSet<HexCoord>();
        }

        public void SetMaxPlacementDistance(int distance)
        {
            maxPlacementDistance = distance;
        }

        /// <summary>
        /// Finds all edges of placed pieces that face unoccupied neighbor hexes.
        /// Returns list of (coord, edge) pairs where coord is the existing piece and edge points to an empty neighbor.
        /// </summary>
        public List<(HexCoord coord, int edge)> GetOpenEdges()
        {
            var openEdges = new List<(HexCoord, int)>();

            foreach (var kvp in map)
            {
                var piece = kvp.Value;
                foreach (int edge in piece.ConnectedEdges)
                {
                    HexCoord neighbor = piece.Coord.GetNeighbor(edge);

                    // Skip neighbors beyond the map boundary
                    if (maxPlacementDistance >= 0 && origin.DistanceTo(neighbor) > maxPlacementDistance)
                        continue;

                    if (!map.ContainsKey(neighbor))
                    {
                        openEdges.Add((piece.Coord, edge));
                    }
                    else if (!nonReplaceableCoords.Contains(neighbor))
                    {
                        // Neighbor is occupied but replaceable
                        openEdges.Add((piece.Coord, edge));
                    }
                }
            }

            return openEdges;
        }

        /// <summary>
        /// For a given piece type, finds all valid placements on the current map.
        /// Groups results by coordinate (one PlacementOption per unique coord, with multiple rotations).
        /// </summary>
        public List<PlacementOption> GetValidPlacements(HexPieceType type)
        {
            var openEdges = GetOpenEdges();
            var optionsByCoord = new Dictionary<HexCoord, PlacementOption>();

            foreach (var (sourceCoord, sourceEdge) in openEdges)
            {
                HexCoord candidateCoord = sourceCoord.GetNeighbor(sourceEdge);
                int entryEdge = HexCoord.OppositeEdge(sourceEdge);

                var rotations = GetRotationVariants(type, entryEdge);

                foreach (var rotation in rotations)
                {
                    if (IsRotationValid(candidateCoord, rotation, entryEdge))
                    {
                        if (!optionsByCoord.TryGetValue(candidateCoord, out var option))
                        {
                            option = new PlacementOption(candidateCoord);
                            optionsByCoord[candidateCoord] = option;
                        }

                        // Avoid duplicate rotations at same coord
                        if (!HasDuplicateRotation(option, rotation))
                        {
                            option.Rotations.Add(rotation);
                        }
                    }
                }
            }

            return new List<PlacementOption>(optionsByCoord.Values);
        }

        /// <summary>
        /// Generates all rotation variants for a piece type given a specific entry edge.
        /// </summary>
        private List<PlacementRotation> GetRotationVariants(HexPieceType type, int entryEdge)
        {
            var variants = new List<PlacementRotation>();
            int opposite = HexCoord.OppositeEdge(entryEdge);

            var pattern = pieceConfigs.ContainsKey(type)
                ? pieceConfigs[type].rotationPattern
                : RotationPattern.Straight;

            switch (pattern)
            {
                case RotationPattern.Straight:
                    // 1 variant: entry + opposite
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { entryEdge, opposite }));
                    break;

                case RotationPattern.Bend:
                    // 5 variants: exit can be any of the 5 non-entry edges
                    for (int e = 0; e < 6; e++)
                    {
                        if (e == entryEdge) continue;
                        variants.Add(new PlacementRotation(entryEdge,
                            new List<int> { entryEdge, e }));
                    }
                    break;

                case RotationPattern.Fork:
                    // 10 variants: entry + any 2 of the remaining 5 edges
                    for (int a = 0; a < 6; a++)
                    {
                        if (a == entryEdge) continue;
                        for (int b = a + 1; b < 6; b++)
                        {
                            if (b == entryEdge) continue;
                            variants.Add(new PlacementRotation(entryEdge,
                                new List<int> { entryEdge, a, b }));
                        }
                    }
                    break;

                case RotationPattern.DeadEnd:
                    // 1 variant: only entry edge
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { entryEdge }));
                    break;

                case RotationPattern.Cross:
                    // C(4,2)=6 variants: entry + opposite + any 2 of the remaining 4 edges
                    {
                        var remaining = new List<int>();
                        for (int e = 0; e < 6; e++)
                        {
                            if (e != entryEdge && e != opposite) remaining.Add(e);
                        }
                        for (int a = 0; a < remaining.Count; a++)
                        {
                            for (int b = a + 1; b < remaining.Count; b++)
                            {
                                variants.Add(new PlacementRotation(entryEdge,
                                    new List<int> { entryEdge, opposite, remaining[a], remaining[b] }));
                            }
                        }
                    }
                    break;

                case RotationPattern.Star:
                    // 5 variants: all 6 edges minus any 1 non-entry edge
                    for (int skip = 0; skip < 6; skip++)
                    {
                        if (skip == entryEdge) continue;
                        var edges = new List<int>();
                        for (int e = 0; e < 6; e++)
                            if (e != skip) edges.Add(e);
                        variants.Add(new PlacementRotation(entryEdge, edges));
                    }
                    break;

                case RotationPattern.Crossroads:
                    // 1 variant: all 6 edges
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { 0, 1, 2, 3, 4, 5 }));
                    break;

                case RotationPattern.Simple:
                    // Straight first, then bends
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { entryEdge, opposite }));
                    for (int e = 0; e < 6; e++)
                    {
                        if (e == entryEdge || e == opposite) continue;
                        variants.Add(new PlacementRotation(entryEdge,
                            new List<int> { entryEdge, e }));
                    }
                    break;
            }

            return variants;
        }

        /// <summary>
        /// Checks that all exit edges (non-entry) of a rotation don't collide with existing pieces
        /// that lack a matching connected edge, and that the placement doesn't disconnect any paths.
        /// </summary>
        private bool IsRotationValid(HexCoord candidateCoord, PlacementRotation rotation, int entryEdge)
        {
            foreach (int edge in rotation.ConnectedEdges)
            {
                if (edge == entryEdge) continue; // Entry edge is already validated

                HexCoord neighborCoord = candidateCoord.GetNeighbor(edge);

                // Reject exit edges that point beyond the map boundary
                if (maxPlacementDistance >= 0 && origin.DistanceTo(neighborCoord) > maxPlacementDistance)
                    return false;

                if (map.ContainsKey(neighborCoord))
                {
                    // There's an existing piece at this neighbor.
                    // The exit edge would need the neighbor to have the opposite edge connected.
                    // If the neighbor doesn't connect back, this rotation is invalid.
                    int requiredNeighborEdge = HexCoord.OppositeEdge(edge);
                    var neighborPiece = map[neighborCoord];
                    if (!neighborPiece.ConnectedEdges.Contains(requiredNeighborEdge))
                    {
                        return false;
                    }
                }
            }

            // When replacing an existing piece, reject rotations identical to the existing piece
            if (map.ContainsKey(candidateCoord))
            {
                var existingEdges = map[candidateCoord].ConnectedEdges;
                if (AreSameEdgeSet(existingEdges, rotation.ConnectedEdges))
                    return false;

                if (WouldDisconnectPaths(candidateCoord, rotation.ConnectedEdges))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether replacing the piece at coord with one having newConnectedEdges
        /// would disconnect any currently reachable piece from the castle.
        /// </summary>
        private bool WouldDisconnectPaths(HexCoord coord, List<int> newConnectedEdges)
        {
            var oldPiece = map[coord];

            // Quick check: are any existing connections being removed?
            bool hasLostConnection = false;
            foreach (int edge in oldPiece.ConnectedEdges)
            {
                if (newConnectedEdges.Contains(edge))
                    continue;

                // This edge is being removed — check if there was an actual bidirectional connection
                var neighbor = coord.GetNeighbor(edge);
                if (map.TryGetValue(neighbor, out var neighborPiece))
                {
                    int requiredEdge = HexCoord.OppositeEdge(edge);
                    if (neighborPiece.ConnectedEdges.Contains(requiredEdge))
                    {
                        hasLostConnection = true;
                        break;
                    }
                }
            }

            if (!hasLostConnection)
                return false; // No connections severed, can't disconnect anything

            // Full simulation: temporarily swap the piece and check reachability
            var currentReachable = GetReachableFromCastle();

            var tempPiece = new HexPieceData(coord, oldPiece.Type, newConnectedEdges);
            map[coord] = tempPiece;
            var newReachable = GetReachableFromCastle();
            map[coord] = oldPiece; // Restore

            // If any previously reachable piece (other than the one being replaced) is now unreachable, reject
            foreach (var reachableCoord in currentReachable)
            {
                if (reachableCoord == coord) continue;
                if (!newReachable.Contains(reachableCoord))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// BFS flood from the castle to find all pieces reachable via bidirectional edge connections.
        /// </summary>
        private HashSet<HexCoord> GetReachableFromCastle()
        {
            var reachable = new HashSet<HexCoord>();
            HexCoord? castleCoord = null;

            foreach (var kvp in map)
            {
                if (kvp.Value.IsCastle)
                {
                    castleCoord = kvp.Key;
                    break;
                }
            }

            if (!castleCoord.HasValue)
                return reachable;

            var queue = new Queue<HexCoord>();
            queue.Enqueue(castleCoord.Value);
            reachable.Add(castleCoord.Value);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!map.TryGetValue(current, out var piece))
                    continue;

                foreach (int edge in piece.ConnectedEdges)
                {
                    var neighbor = current.GetNeighbor(edge);
                    if (reachable.Contains(neighbor))
                        continue;
                    if (!map.TryGetValue(neighbor, out var neighborPiece))
                        continue;

                    int requiredEdge = HexCoord.OppositeEdge(edge);
                    if (!neighborPiece.ConnectedEdges.Contains(requiredEdge))
                        continue;

                    reachable.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return reachable;
        }

        private bool AreSameEdgeSet(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            foreach (int edge in a)
            {
                if (!b.Contains(edge)) return false;
            }
            return true;
        }

        private bool HasDuplicateRotation(PlacementOption option, PlacementRotation newRotation)
        {
            foreach (var existing in option.Rotations)
            {
                if (existing.ConnectedEdges.Count != newRotation.ConnectedEdges.Count)
                    continue;

                bool same = true;
                for (int i = 0; i < existing.ConnectedEdges.Count; i++)
                {
                    if (existing.ConnectedEdges[i] != newRotation.ConnectedEdges[i])
                    {
                        same = false;
                        break;
                    }
                }
                if (same) return true;
            }
            return false;
        }
    }
}
