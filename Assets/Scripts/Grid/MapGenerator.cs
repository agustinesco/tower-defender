using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Grid
{
    public class MapGenerator
    {
        private Dictionary<HexCoord, HexPieceData> pieces = new Dictionary<HexCoord, HexPieceData>();
        private System.Random random;

        // Fork generation tracking
        private float currentForkChance;
        private bool forkGeneratedThisExpansion;

        public bool ForkGeneratedLastExpansion => forkGeneratedThisExpansion;

        public MapGenerator(int seed = -1)
        {
            random = seed >= 0 ? new System.Random(seed) : new System.Random();
        }

        public Dictionary<HexCoord, HexPieceData> Generate()
        {
            pieces.Clear();

            // Step 1: Place castle at center with random exit edge
            int castleExitEdge = random.Next(6);
            var castle = new HexPieceData(
                new HexCoord(0, 0),
                HexPieceType.Castle,
                new List<int> { castleExitEdge }
            );
            pieces[castle.Coord] = castle;

            // Step 2: Generate path from castle
            GeneratePath(castle.Coord, castleExitEdge, 6);

            return new Dictionary<HexCoord, HexPieceData>(pieces);
        }

        private void GeneratePath(HexCoord fromCoord, int exitEdge, int remainingPieces)
        {
            if (remainingPieces <= 0) return;

            HexCoord newCoord = fromCoord.GetNeighbor(exitEdge);
            int entryEdge = HexCoord.OppositeEdge(exitEdge);

            // Check for collision
            if (pieces.ContainsKey(newCoord))
            {
                // Force dead-end at previous piece
                return;
            }

            // Last piece is always a dead-end
            if (remainingPieces == 1)
            {
                var deadEnd = new HexPieceData(newCoord, HexPieceType.DeadEnd, new List<int> { entryEdge });
                pieces[newCoord] = deadEnd;
                return;
            }

            // Decide piece type: 60% straight, 30% bend, 10% fork
            float roll = (float)random.NextDouble();
            HexPieceType type;
            List<int> edges = new List<int> { entryEdge };

            if (roll < 0.6f)
            {
                type = HexPieceType.Straight;
                int newExitEdge = HexCoord.OppositeEdge(entryEdge);
                edges.Add(newExitEdge);

                var piece = new HexPieceData(newCoord, type, edges);
                pieces[newCoord] = piece;

                GeneratePath(newCoord, newExitEdge, remainingPieces - 1);
            }
            else if (roll < 0.9f)
            {
                type = HexPieceType.Bend;
                // Pick adjacent edge (not opposite)
                int offset = random.Next(2) == 0 ? 1 : -1;
                int newExitEdge = ((entryEdge + 3 + offset) % 6 + 6) % 6;
                edges.Add(newExitEdge);

                var piece = new HexPieceData(newCoord, type, edges);
                pieces[newCoord] = piece;

                GeneratePath(newCoord, newExitEdge, remainingPieces - 1);
            }
            else
            {
                type = HexPieceType.Fork;
                int exitEdge1 = HexCoord.OppositeEdge(entryEdge);
                int exitEdge2 = ((entryEdge + 2) % 6 + 6) % 6;
                edges.Add(exitEdge1);
                edges.Add(exitEdge2);

                var piece = new HexPieceData(newCoord, type, edges);
                pieces[newCoord] = piece;

                // Main path continues
                int mainPieces = remainingPieces / 2;
                int branchPieces = remainingPieces - mainPieces - 1;

                GeneratePath(newCoord, exitEdge1, mainPieces);
                GeneratePath(newCoord, exitEdge2, branchPieces > 0 ? branchPieces : 1);
            }
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

        /// <summary>
        /// Expands the map from a dead end, converting it to a path and adding new tiles.
        /// </summary>
        /// <param name="existingPieces">Current map pieces</param>
        /// <param name="deadEndCoord">Coordinate of the dead end to expand from</param>
        /// <param name="tilesToAdd">Number of tiles to add (default 3)</param>
        /// <param name="forkChance">Chance to generate a fork (0-1, default 0.1)</param>
        /// <returns>List of new piece data that was generated</returns>
        public List<HexPieceData> ExpandFromDeadEnd(Dictionary<HexCoord, HexPieceData> existingPieces, HexCoord deadEndCoord, int tilesToAdd = 3, float forkChance = 0.1f)
        {
            pieces = new Dictionary<HexCoord, HexPieceData>(existingPieces);
            var newPieces = new List<HexPieceData>();

            // Reset fork tracking for this expansion
            currentForkChance = Mathf.Clamp01(forkChance);
            forkGeneratedThisExpansion = false;

            if (!pieces.TryGetValue(deadEndCoord, out var deadEnd) || !deadEnd.IsSpawnPoint)
            {
                Debug.LogWarning($"MapGenerator: {deadEndCoord} is not a valid dead end");
                return newPieces;
            }

            // Get the entry edge of the dead end (it only has one connected edge)
            int entryEdge = deadEnd.ConnectedEdges[0];

            // Find a valid exit edge that doesn't collide with existing pieces
            int exitEdge = FindValidExitEdge(deadEndCoord, entryEdge);
            if (exitEdge < 0)
            {
                Debug.LogWarning($"MapGenerator: No valid exit edge found for {deadEndCoord}");
                return newPieces;
            }

            // Convert dead end to a path piece (straight or bend depending on exit edge)
            HexPieceType newType = (exitEdge == HexCoord.OppositeEdge(entryEdge))
                ? HexPieceType.Straight
                : HexPieceType.Bend;

            var convertedPiece = new HexPieceData(
                deadEndCoord,
                newType,
                new List<int> { entryEdge, exitEdge }
            );
            pieces[deadEndCoord] = convertedPiece;
            newPieces.Add(convertedPiece);

            // Generate new path from the converted piece
            GenerateExpansionPath(deadEndCoord, exitEdge, tilesToAdd, newPieces);

            return newPieces;
        }

        private int FindValidExitEdge(HexCoord coord, int entryEdge)
        {
            // Try opposite edge first (straight path)
            int oppositeEdge = HexCoord.OppositeEdge(entryEdge);
            if (!pieces.ContainsKey(coord.GetNeighbor(oppositeEdge)))
            {
                return oppositeEdge;
            }

            // Try adjacent edges (bend path)
            int[] adjacentOffsets = { 1, -1, 2, -2 };
            foreach (int offset in adjacentOffsets)
            {
                int edge = ((entryEdge + 3 + offset) % 6 + 6) % 6;
                if (edge != entryEdge && !pieces.ContainsKey(coord.GetNeighbor(edge)))
                {
                    return edge;
                }
            }

            return -1; // No valid edge found
        }

        private void GenerateExpansionPath(HexCoord fromCoord, int exitEdge, int remainingPieces, List<HexPieceData> newPieces)
        {
            if (remainingPieces <= 0) return;

            HexCoord newCoord = fromCoord.GetNeighbor(exitEdge);
            int entryEdge = HexCoord.OppositeEdge(exitEdge);

            // Check for collision
            if (pieces.ContainsKey(newCoord))
            {
                return;
            }

            // Last piece is always a dead-end
            if (remainingPieces == 1)
            {
                var deadEnd = new HexPieceData(newCoord, HexPieceType.DeadEnd, new List<int> { entryEdge });
                pieces[newCoord] = deadEnd;
                newPieces.Add(deadEnd);
                return;
            }

            // Decide piece type based on dynamic fork chance
            // Remaining chance is split: ~67% straight, ~33% bend
            float roll = (float)random.NextDouble();
            float nonForkChance = 1f - currentForkChance;
            float straightChance = nonForkChance * 0.67f;
            float bendChance = nonForkChance * 0.33f;

            HexPieceType type;
            List<int> edges = new List<int> { entryEdge };

            if (roll < straightChance)
            {
                type = HexPieceType.Straight;
                int newExitEdge = HexCoord.OppositeEdge(entryEdge);

                // Check if straight path is blocked
                if (pieces.ContainsKey(newCoord.GetNeighbor(newExitEdge)))
                {
                    // Force a bend or dead end
                    int altExit = FindValidExitEdge(newCoord, entryEdge);
                    if (altExit < 0)
                    {
                        var deadEnd = new HexPieceData(newCoord, HexPieceType.DeadEnd, new List<int> { entryEdge });
                        pieces[newCoord] = deadEnd;
                        newPieces.Add(deadEnd);
                        return;
                    }
                    newExitEdge = altExit;
                    type = HexPieceType.Bend;
                }

                edges.Add(newExitEdge);
                var piece = new HexPieceData(newCoord, type, edges);
                pieces[newCoord] = piece;
                newPieces.Add(piece);

                GenerateExpansionPath(newCoord, newExitEdge, remainingPieces - 1, newPieces);
            }
            else if (roll < straightChance + bendChance)
            {
                type = HexPieceType.Bend;
                int offset = random.Next(2) == 0 ? 1 : -1;
                int newExitEdge = ((entryEdge + 3 + offset) % 6 + 6) % 6;

                // Check if bend is blocked
                if (pieces.ContainsKey(newCoord.GetNeighbor(newExitEdge)))
                {
                    int altExit = FindValidExitEdge(newCoord, entryEdge);
                    if (altExit < 0)
                    {
                        var deadEnd = new HexPieceData(newCoord, HexPieceType.DeadEnd, new List<int> { entryEdge });
                        pieces[newCoord] = deadEnd;
                        newPieces.Add(deadEnd);
                        return;
                    }
                    newExitEdge = altExit;
                }

                edges.Add(newExitEdge);
                var piece = new HexPieceData(newCoord, type, edges);
                pieces[newCoord] = piece;
                newPieces.Add(piece);

                GenerateExpansionPath(newCoord, newExitEdge, remainingPieces - 1, newPieces);
            }
            else
            {
                // Fork - only if we have enough remaining pieces
                if (remainingPieces < 2)
                {
                    var deadEnd = new HexPieceData(newCoord, HexPieceType.DeadEnd, new List<int> { entryEdge });
                    pieces[newCoord] = deadEnd;
                    newPieces.Add(deadEnd);
                    return;
                }

                type = HexPieceType.Fork;
                int exitEdge1 = HexCoord.OppositeEdge(entryEdge);
                int exitEdge2 = ((entryEdge + 2) % 6 + 6) % 6;
                edges.Add(exitEdge1);
                edges.Add(exitEdge2);

                var piece = new HexPieceData(newCoord, type, edges);
                pieces[newCoord] = piece;
                newPieces.Add(piece);

                // Mark that a fork was generated this expansion
                forkGeneratedThisExpansion = true;
                Debug.Log($"Fork generated at {newCoord}! Fork chance was {currentForkChance:P0}");

                int mainPieces = (remainingPieces - 1) / 2;
                int branchPieces = remainingPieces - 1 - mainPieces;

                GenerateExpansionPath(newCoord, exitEdge1, mainPieces > 0 ? mainPieces : 1, newPieces);
                GenerateExpansionPath(newCoord, exitEdge2, branchPieces > 0 ? branchPieces : 1, newPieces);
            }
        }
    }
}