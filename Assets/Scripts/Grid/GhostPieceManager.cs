using System.Collections.Generic;
using UnityEngine;
using TowerDefense.UI;

namespace TowerDefense.Grid
{
    public class GhostPieceManager : MonoBehaviour
    {
        private Sprite goblinCampSprite;

        private Dictionary<HexCoord, HexPiece> ghosts = new Dictionary<HexCoord, HexPiece>();
        private Dictionary<HexCoord, PlacementOption> optionsByCoord = new Dictionary<HexCoord, PlacementOption>();
        private Dictionary<HexCoord, int> currentRotationIndex = new Dictionary<HexCoord, int>();
        private Dictionary<HexCoord, GameObject> warningIndicators = new Dictionary<HexCoord, GameObject>();

        private Material ghostHexMaterial;
        private Material ghostPathMaterial;
        private HexCoord? highlightedCoord;
        private HashSet<HexCoord> hiddenSpawnerPositions = new HashSet<HexCoord>();

        public void Initialize(Sprite campSprite = null)
        {
            goblinCampSprite = campSprite;
            ghostHexMaterial = TowerDefense.Core.MaterialCache.CreateTransparent(new Color(0.75f, 0.75f, 0.75f, 0.12f));
            ghostPathMaterial = TowerDefense.Core.MaterialCache.CreateTransparent(new Color(1f, 0.95f, 0.6f, 0.5f));
        }

        public void SetHiddenSpawners(HashSet<HexCoord> spawners)
        {
            hiddenSpawnerPositions = spawners ?? new HashSet<HexCoord>();
        }

        public void ShowGhosts(List<PlacementOption> options)
        {
            HideAllGhosts();

            foreach (var option in options)
            {
                if (option.Rotations.Count == 0) continue;

                optionsByCoord[option.Coord] = option;
                currentRotationIndex[option.Coord] = 0;

                var rotation = option.Rotations[0];
                var pieceData = new HexPieceData(option.Coord, HexPieceType.Straight, rotation.ConnectedEdges);

                GameObject ghostObj = new GameObject($"Ghost_{option.Coord}");
                ghostObj.transform.SetParent(transform);
                var ghost = ghostObj.AddComponent<HexPiece>();
                ghost.InitializeAsGhost(pieceData, ghostHexMaterial, ghostPathMaterial);

                ghosts[option.Coord] = ghost;

                // Check if this ghost's edges connect to a hidden spawner
                if (ConnectsToHiddenSpawner(option.Coord, rotation.ConnectedEdges))
                {
                    CreateWarningIndicator(option.Coord, ghost.transform.position);
                }
            }
        }

        public void HideAllGhosts()
        {
            foreach (var kvp in ghosts)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            ghosts.Clear();
            optionsByCoord.Clear();
            currentRotationIndex.Clear();
            highlightedCoord = null;

            foreach (var kvp in warningIndicators)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            warningIndicators.Clear();
        }

        public void HighlightGhost(HexCoord coord)
        {
            ClearHighlight();

            if (ghosts.TryGetValue(coord, out var ghost))
            {
                ghost.SetGhostHighlight(true);
                highlightedCoord = coord;
            }
        }

        public void ClearHighlight()
        {
            if (highlightedCoord.HasValue && ghosts.TryGetValue(highlightedCoord.Value, out var ghost))
            {
                ghost.SetGhostHighlight(false);
            }
            highlightedCoord = null;
        }

        public void CycleRotation(HexCoord coord)
        {
            if (!optionsByCoord.TryGetValue(coord, out var option)) return;
            if (!ghosts.TryGetValue(coord, out var ghost)) return;
            if (option.Rotations.Count <= 1) return;

            int idx = currentRotationIndex[coord];
            idx = (idx + 1) % option.Rotations.Count;
            currentRotationIndex[coord] = idx;

            var rotation = option.Rotations[idx];
            ghost.SetGhostRotation(rotation.ConnectedEdges);

            // Re-evaluate warning indicator for new rotation
            RemoveWarningIndicator(coord);
            if (ConnectsToHiddenSpawner(coord, rotation.ConnectedEdges))
            {
                CreateWarningIndicator(coord, ghost.transform.position);
            }
        }

        public PlacementRotation GetCurrentPlacement(HexCoord coord)
        {
            if (!optionsByCoord.TryGetValue(coord, out var option)) return null;
            if (!currentRotationIndex.TryGetValue(coord, out int idx)) return null;

            return option.Rotations[idx];
        }

        public HexCoord? GetNearestGhostCoord(Vector3 worldPos, float threshold)
        {
            HexCoord? nearest = null;
            float minDist = threshold;

            foreach (var kvp in ghosts)
            {
                if (kvp.Value == null) continue;

                float dist = Vector3.Distance(worldPos, kvp.Value.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = kvp.Key;
                }
            }

            return nearest;
        }

        public void UpdateHoldProgress(HexCoord coord, float progress)
        {
            if (ghosts.TryGetValue(coord, out var ghost))
            {
                ghost.SetHoldProgress(progress);
            }
        }

        public bool HasGhostAt(HexCoord coord)
        {
            return ghosts.ContainsKey(coord);
        }

        public int GhostCount => ghosts.Count;

        private bool ConnectsToHiddenSpawner(HexCoord coord, List<int> connectedEdges)
        {
            foreach (int edge in connectedEdges)
            {
                HexCoord neighbor = coord.GetNeighbor(edge);
                if (hiddenSpawnerPositions.Contains(neighbor))
                    return true;
            }
            return false;
        }

        private void RemoveWarningIndicator(HexCoord coord)
        {
            if (warningIndicators.TryGetValue(coord, out var indicator))
            {
                if (indicator != null) Destroy(indicator);
                warningIndicators.Remove(coord);
            }
        }

        private void CreateWarningIndicator(HexCoord coord, Vector3 worldPos)
        {
            var indicator = new GameObject("SpawnerWarning");
            indicator.transform.SetParent(transform);
            indicator.transform.position = worldPos + Vector3.up * 5f;
            indicator.transform.localScale = Vector3.one;

            var sr = indicator.AddComponent<SpriteRenderer>();
            if (goblinCampSprite != null)
                sr.sprite = goblinCampSprite;

            indicator.AddComponent<BillboardSprite>();

            warningIndicators[coord] = indicator;
        }

        private void OnDestroy()
        {
            if (ghostHexMaterial != null) Destroy(ghostHexMaterial);
            if (ghostPathMaterial != null) Destroy(ghostPathMaterial);
        }
    }
}
