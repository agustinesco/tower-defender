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
                    // 2 variants: exit at opposite+1 or opposite-1
                    int bendExit1 = ((opposite + 1) % 6 + 6) % 6;
                    int bendExit2 = ((opposite - 1) % 6 + 6) % 6;
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { entryEdge, bendExit1 }));
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { entryEdge, bendExit2 }));
                    break;

                case RotationPattern.Fork:
                    // 2 variants: opposite + one adjacent offset
                    int forkSide1 = ((opposite + 1) % 6 + 6) % 6;
                    int forkSide2 = ((opposite - 1) % 6 + 6) % 6;
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { entryEdge, opposite, forkSide1 }));
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { entryEdge, opposite, forkSide2 }));
                    break;

                case RotationPattern.DeadEnd:
                    // 1 variant: only entry edge
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { entryEdge }));
                    break;

                case RotationPattern.Cross:
                    // 2 variants: entry + opposite + 2 adjacent edges
                    int crossOpp = HexCoord.OppositeEdge(entryEdge);
                    // Variant 1: both adjacent to opposite
                    int crossA1 = ((crossOpp + 1) % 6 + 6) % 6;
                    int crossA2 = ((crossOpp - 1) % 6 + 6) % 6;
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { entryEdge, crossOpp, crossA1, crossA2 }));
                    // Variant 2: both adjacent to entry
                    int crossB1 = ((entryEdge + 1) % 6 + 6) % 6;
                    int crossB2 = ((entryEdge - 1) % 6 + 6) % 6;
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { entryEdge, crossOpp, crossB1, crossB2 }));
                    break;

                case RotationPattern.Star:
                    // 2 variants: all edges except one adjacent to entry
                    int starOpp = HexCoord.OppositeEdge(entryEdge);
                    int starSkip1 = ((entryEdge + 1) % 6 + 6) % 6;
                    int starSkip2 = ((entryEdge - 1) % 6 + 6) % 6;
                    // Variant 1: exclude entry+1
                    {
                        var edges = new List<int>();
                        for (int e = 0; e < 6; e++)
                            if (e != starSkip1) edges.Add(e);
                        variants.Add(new PlacementRotation(entryEdge, edges));
                    }
                    // Variant 2: exclude entry-1
                    {
                        var edges = new List<int>();
                        for (int e = 0; e < 6; e++)
                            if (e != starSkip2) edges.Add(e);
                        variants.Add(new PlacementRotation(entryEdge, edges));
                    }
                    break;

                case RotationPattern.Crossroads:
                    // 1 variant: all 6 edges
                    variants.Add(new PlacementRotation(entryEdge,
                        new List<int> { 0, 1, 2, 3, 4, 5 }));
                    break;
            }

            return variants;
        }

        /// <summary>
        /// Checks that all exit edges (non-entry) of a rotation don't collide with existing pieces
        /// that lack a matching connected edge.
        /// </summary>
        private bool IsRotationValid(HexCoord candidateCoord, PlacementRotation rotation, int entryEdge)
        {
            foreach (int edge in rotation.ConnectedEdges)
            {
                if (edge == entryEdge) continue; // Entry edge is already validated

                HexCoord neighborCoord = candidateCoord.GetNeighbor(edge);

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
