using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TowerDefense.Grid;
using TowerDefense.Data;
using TowerDefense.Core;

namespace TowerDefense.UI
{
    public class MapPreviewController : MonoBehaviour
    {
        [Header("Map Configs")]
        [SerializeField] private List<MapConfig> mapConfigs;

        [Header("Materials")]
        [SerializeField] private Material hexBaseMaterial;
        [SerializeField] private Material hexPathMaterial;
        [SerializeField] private Material castleMaterial;

        [Header("Sprites")]
        [SerializeField] private Sprite goblinCampSprite;
        [SerializeField] private Sprite goblinSprite;
        [SerializeField] private Sprite oreSprite;
        [SerializeField] private Sprite gemsSprite;
        [SerializeField] private Sprite florpusSprite;
        [SerializeField] private Sprite adamantiteSprite;

        [Header("Prefabs")]
        [SerializeField] private GameObject hexPiecePrefab;

        [Header("Piece Configs")]
        [SerializeField] private List<HexPieceConfig> pieceConfigs;

        // UI references
        private Dropdown configDropdown;
        private Text seedLabel;

        // State
        private Dictionary<HexPieceType, HexPieceConfig> pieceConfigLookup;
        private Dictionary<HexCoord, HexPiece> hexPieces = new Dictionary<HexCoord, HexPiece>();
        private Dictionary<HexCoord, HexPieceData> mapData = new Dictionary<HexCoord, HexPieceData>();
        private Dictionary<HexCoord, OrePatch> orePatches = new Dictionary<HexCoord, OrePatch>();
        private HashSet<HexCoord> hiddenSpawners = new HashSet<HexCoord>();
        private List<GameObject> zoneRings = new List<GameObject>();
        private List<GameObject> spawnIndicators = new List<GameObject>();
        private List<GameObject> orePatchMarkers = new List<GameObject>();
        private List<GameObject> hiddenSpawnerMarkers = new List<GameObject>();
        private TerrainManager terrainManager;
        private int currentSeed = -1;

        private void Awake()
        {
            BuildPieceConfigLookup();
            CreateUI();
        }

        private void Start()
        {
            if (mapConfigs != null && mapConfigs.Count > 0)
                GeneratePreview(mapConfigs[0], -1);
        }

        private void BuildPieceConfigLookup()
        {
            pieceConfigLookup = new Dictionary<HexPieceType, HexPieceConfig>();
            if (pieceConfigs == null) return;
            foreach (var config in pieceConfigs)
            {
                if (config != null)
                    pieceConfigLookup[config.pieceType] = config;
            }
        }

        private void CreateUI()
        {
            var canvas = new GameObject("PreviewUI_Canvas");
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 10;
            canvas.AddComponent<CanvasScaler>();
            canvas.AddComponent<GraphicRaycaster>();

            // EventSystem required for UI interaction
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            var panel = CreatePanel(canvas.transform);

            // Dropdown
            configDropdown = CreateDropdown(panel.transform);
            PopulateDropdown();

            // Load button
            CreateButton(panel.transform, "Load", OnLoadClicked);

            // Regenerate button
            CreateButton(panel.transform, "Regenerate", OnRegenerateClicked);

            // Seed label
            var seedObj = new GameObject("SeedLabel");
            seedObj.transform.SetParent(panel.transform, false);
            seedLabel = seedObj.AddComponent<Text>();
            seedLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            seedLabel.fontSize = 14;
            seedLabel.color = Color.white;
            seedLabel.text = "Seed: random";
            var seedLE = seedObj.AddComponent<LayoutElement>();
            seedLE.preferredHeight = 30;
        }

        private GameObject CreatePanel(Transform parent)
        {
            var panel = new GameObject("Panel");
            panel.transform.SetParent(parent, false);

            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, -10f);
            rt.sizeDelta = new Vector2(250f, 200f);

            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.7f);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 6f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return panel;
        }

        private Dropdown CreateDropdown(Transform parent)
        {
            var ddObj = new GameObject("ConfigDropdown");
            ddObj.transform.SetParent(parent, false);

            var ddRT = ddObj.AddComponent<RectTransform>();
            var ddLE = ddObj.AddComponent<LayoutElement>();
            ddLE.preferredHeight = 35;

            var ddImg = ddObj.AddComponent<Image>();
            ddImg.color = new Color(0.2f, 0.2f, 0.2f);

            var dd = ddObj.AddComponent<Dropdown>();

            // Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(ddObj.transform, false);
            var labelRT = labelObj.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(8f, 0f);
            labelRT.offsetMax = new Vector2(-25f, 0f);
            var labelText = labelObj.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            dd.captionText = labelText;

            // Template
            var templateObj = new GameObject("Template");
            templateObj.transform.SetParent(ddObj.transform, false);
            var templateRT = templateObj.AddComponent<RectTransform>();
            templateRT.anchorMin = new Vector2(0f, 0f);
            templateRT.anchorMax = new Vector2(1f, 0f);
            templateRT.pivot = new Vector2(0.5f, 1f);
            templateRT.anchoredPosition = Vector2.zero;
            templateRT.sizeDelta = new Vector2(0f, 150f);
            var templateImg = templateObj.AddComponent<Image>();
            templateImg.color = new Color(0.15f, 0.15f, 0.15f);
            var scrollRect = templateObj.AddComponent<ScrollRect>();

            // Viewport
            var viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(templateObj.transform, false);
            var vpRT = viewportObj.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.sizeDelta = Vector2.zero;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            viewportObj.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);
            viewportObj.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = vpRT;

            // Content
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            var contentRT = contentObj.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.sizeDelta = new Vector2(0f, 28f);
            scrollRect.content = contentRT;

            // Item
            var itemObj = new GameObject("Item");
            itemObj.transform.SetParent(contentObj.transform, false);
            var itemRT = itemObj.AddComponent<RectTransform>();
            itemRT.anchorMin = new Vector2(0f, 0.5f);
            itemRT.anchorMax = new Vector2(1f, 0.5f);
            itemRT.sizeDelta = new Vector2(0f, 28f);

            var itemToggle = itemObj.AddComponent<Toggle>();

            var itemBg = new GameObject("Item Background");
            itemBg.transform.SetParent(itemObj.transform, false);
            var bgRT = itemBg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            itemBg.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f);

            var itemLabel = new GameObject("Item Label");
            itemLabel.transform.SetParent(itemObj.transform, false);
            var ilRT = itemLabel.AddComponent<RectTransform>();
            ilRT.anchorMin = Vector2.zero;
            ilRT.anchorMax = Vector2.one;
            ilRT.offsetMin = new Vector2(8f, 0f);
            ilRT.offsetMax = new Vector2(-8f, 0f);
            var ilText = itemLabel.AddComponent<Text>();
            ilText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ilText.fontSize = 14;
            ilText.color = Color.white;
            ilText.alignment = TextAnchor.MiddleLeft;

            dd.itemText = ilText;
            itemToggle.targetGraphic = bgRT.GetComponent<Image>();

            dd.template = templateRT;
            templateObj.SetActive(false);

            return dd;
        }

        private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);

            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 35;

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.6f);

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            var text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
        }

        private void PopulateDropdown()
        {
            if (configDropdown == null || mapConfigs == null) return;
            configDropdown.ClearOptions();
            var options = new List<string>();
            foreach (var config in mapConfigs)
            {
                options.Add(config != null ? config.name : "(null)");
            }
            configDropdown.AddOptions(options);
        }

        private void OnLoadClicked()
        {
            int idx = configDropdown != null ? configDropdown.value : 0;
            if (mapConfigs == null || idx < 0 || idx >= mapConfigs.Count) return;
            GeneratePreview(mapConfigs[idx], -1);
        }

        private void OnRegenerateClicked()
        {
            int idx = configDropdown != null ? configDropdown.value : 0;
            if (mapConfigs == null || idx < 0 || idx >= mapConfigs.Count) return;
            int newSeed = Random.Range(0, int.MaxValue);
            GeneratePreview(mapConfigs[idx], newSeed);
        }

        private void GeneratePreview(MapConfig config, int seed)
        {
            ClearPreviousPreview();

            currentSeed = seed;
            if (seedLabel != null)
                seedLabel.text = seed >= 0 ? $"Seed: {seed}" : "Seed: random";

            var mapGen = new MapGenerator(seed);

            // 1. Generate castle + starting path
            mapData = mapGen.GenerateInitialCastle();
            int tileCount = config.startingTiles;
            var startingPieces = mapGen.GenerateStartingPath(tileCount);
            foreach (var piece in startingPieces)
                mapData[piece.Coord] = piece;

            // 2. Hidden spawners
            hiddenSpawners.Clear();
            if (config.hiddenSpawnerCount > 0)
            {
                hiddenSpawners = mapGen.GenerateHiddenSpawners(
                    config.hiddenSpawnerCount,
                    config.hiddenSpawnerMinDistance,
                    config.hiddenSpawnerMaxDistance);
            }

            // 3. Ore patches
            int[] zoneBounds = config.GetZoneBoundaries();
            if (config.zones != null && config.zones.Length > 0)
            {
                orePatches = mapGen.GenerateOrePatches(
                    config.oreMinDistance, config.oreMaxDistance,
                    zoneBounds, config.zones,
                    config.guaranteeStartingOre, config.guaranteedOreType);
            }
            else
            {
                int zone1Boundary = zoneBounds.Length > 0 ? zoneBounds[0] : 3;
                orePatches = mapGen.GenerateOrePatches(minDistance: 2, maxDistance: 6, zoneBoundary: zone1Boundary);
            }

            // 4. Create visuals
            foreach (var kvp in mapData)
                CreateHexPiece(kvp.Value);

            CreateOrePatchMarkers();
            CreateZoneRings(zoneBounds);
            CreateSpawnIndicators(mapData);
            CreateHiddenSpawnerMarkers();

            // 5. Terrain
            var terrainObj = new GameObject("TerrainManager");
            terrainManager = terrainObj.AddComponent<TerrainManager>();
            terrainManager.Initialize();
            if (zoneBounds.Length > 0)
                terrainManager.SetMaxPlacementDistance(zoneBounds[zoneBounds.Length - 1]);
            terrainManager.UpdateTerrain(mapData);

            // 6. Fit camera
            FitCamera();
        }

        private void ClearPreviousPreview()
        {
            foreach (var kvp in hexPieces)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            hexPieces.Clear();
            mapData.Clear();
            orePatches.Clear();
            hiddenSpawners.Clear();

            foreach (var ring in zoneRings)
            {
                if (ring != null) Destroy(ring);
            }
            zoneRings.Clear();

            foreach (var ind in spawnIndicators)
            {
                if (ind != null) Destroy(ind);
            }
            spawnIndicators.Clear();

            foreach (var marker in orePatchMarkers)
            {
                if (marker != null) Destroy(marker);
            }
            orePatchMarkers.Clear();

            foreach (var marker in hiddenSpawnerMarkers)
            {
                if (marker != null) Destroy(marker);
            }
            hiddenSpawnerMarkers.Clear();

            if (terrainManager != null)
            {
                Destroy(terrainManager.gameObject);
                terrainManager = null;
            }
        }

        private void CreateHexPiece(HexPieceData data)
        {
            GameObject pieceObj;
            if (hexPiecePrefab != null)
                pieceObj = Instantiate(hexPiecePrefab);
            else
                pieceObj = new GameObject($"HexPiece_{data.Coord}");

            var piece = pieceObj.GetComponent<HexPiece>();
            if (piece == null)
                piece = pieceObj.AddComponent<HexPiece>();

            HexPieceConfig config = null;
            pieceConfigLookup?.TryGetValue(data.Type, out config);

            piece.Initialize(data, config, hexBaseMaterial, hexPathMaterial, castleMaterial);
            hexPieces[data.Coord] = piece;
        }

        private void CreateOrePatchMarkers()
        {
            var resourceColors = new Dictionary<ResourceType, Color>
            {
                { ResourceType.IronOre, new Color(0.7f, 0.7f, 0.75f) },
                { ResourceType.Gems, new Color(0.5f, 0.2f, 1f) },
                { ResourceType.Florpus, new Color(0.2f, 0.9f, 0.5f) },
                { ResourceType.Adamantite, new Color(1f, 0.3f, 0.3f) }
            };

            var resourceSprites = new Dictionary<ResourceType, Sprite>
            {
                { ResourceType.IronOre, oreSprite },
                { ResourceType.Gems, gemsSprite },
                { ResourceType.Florpus, florpusSprite },
                { ResourceType.Adamantite, adamantiteSprite != null ? adamantiteSprite : oreSprite }
            };

            foreach (var kvp in orePatches)
            {
                Vector3 worldPos = HexGrid.HexToWorld(kvp.Key);

                GameObject marker = new GameObject($"OrePatch_{kvp.Value.ResourceType}_{kvp.Key}");
                marker.transform.position = worldPos + Vector3.up * 5f;
                marker.transform.localScale = Vector3.one;

                var sr = marker.AddComponent<SpriteRenderer>();
                var resType = kvp.Value.ResourceType;
                if (resourceSprites.TryGetValue(resType, out var sprite) && sprite != null)
                    sr.sprite = sprite;
                else if (oreSprite != null)
                    sr.sprite = oreSprite;

                Color c = resourceColors.TryGetValue(resType, out var rc) ? rc : Color.white;
                sr.color = c;

                marker.AddComponent<BillboardSprite>();

                // Hex outline ring at ground level
                var ringObj = new GameObject("HexRing");
                ringObj.transform.SetParent(marker.transform);
                ringObj.transform.position = worldPos + Vector3.up * 0.15f;

                var lr = ringObj.AddComponent<LineRenderer>();
                lr.loop = true;
                lr.useWorldSpace = true;
                lr.startWidth = 0.3f;
                lr.endWidth = 0.3f;
                lr.positionCount = 6;

                Color ringColor = c;
                ringColor.a = 0.25f;
                lr.material = MaterialCache.CreateUnlit(ringColor);

                Vector3[] corners = HexGrid.GetHexCorners(kvp.Key);
                for (int i = 0; i < 6; i++)
                    lr.SetPosition(i, new Vector3(corners[i].x, 0.15f, corners[i].z));

                orePatchMarkers.Add(marker);
            }
        }

        private void CreateZoneRings(int[] zoneBounds)
        {
            Color[] ringColors = new Color[]
            {
                new Color(0.8f, 0.8f, 0.2f, 0.6f),
                new Color(0.8f, 0.5f, 0.2f, 0.6f),
                new Color(0.8f, 0.2f, 0.2f, 0.6f),
                new Color(0.6f, 0.1f, 0.1f, 0.6f),
            };

            for (int i = 0; i < zoneBounds.Length; i++)
            {
                bool isOuterBoundary = i == zoneBounds.Length - 1;
                Color color = isOuterBoundary
                    ? new Color(0.8f, 0.15f, 0.15f, 1f)
                    : ringColors[Mathf.Min(i, ringColors.Length - 1)];
                float width = isOuterBoundary ? 3f : 1.5f;
                string ringName = isOuterBoundary ? "BoundaryRing" : $"ZoneRing_{i + 2}";

                CreateHexZoneRing(zoneBounds[i], color, width, ringName);
            }
        }

        private void CreateHexZoneRing(int distance, Color color, float width, string ringName)
        {
            var origin = new HexCoord(0, 0);
            var segments = new List<(Vector3 a, Vector3 b)>();

            for (int q = -distance; q <= distance; q++)
            {
                for (int r = -distance; r <= distance; r++)
                {
                    var hex = new HexCoord(q, r);
                    if (origin.DistanceTo(hex) != distance) continue;

                    var corners = HexGrid.GetHexCorners(hex);
                    for (int e = 0; e < 6; e++)
                    {
                        var neighbor = hex.GetNeighbor(e);
                        if (origin.DistanceTo(neighbor) > distance)
                        {
                            var a = corners[e];
                            var b = corners[(e + 1) % 6];
                            a.y = 0.15f;
                            b.y = 0.15f;
                            segments.Add((a, b));
                        }
                    }
                }
            }

            if (segments.Count == 0) return;

            // Build adjacency
            var adjacency = new Dictionary<long, List<int>>();
            for (int i = 0; i < segments.Count; i++)
            {
                long keyA = HexEdgePosKey(segments[i].a);
                long keyB = HexEdgePosKey(segments[i].b);
                if (!adjacency.TryGetValue(keyA, out var listA))
                {
                    listA = new List<int>();
                    adjacency[keyA] = listA;
                }
                listA.Add(i);
                if (!adjacency.TryGetValue(keyB, out var listB))
                {
                    listB = new List<int>();
                    adjacency[keyB] = listB;
                }
                listB.Add(i);
            }

            // Chain segments into ordered closed path
            var points = new List<Vector3>();
            var used = new HashSet<int>();
            used.Add(0);
            points.Add(segments[0].a);
            var currentEnd = segments[0].b;

            while (used.Count < segments.Count)
            {
                points.Add(currentEnd);
                long key = HexEdgePosKey(currentEnd);
                if (!adjacency.TryGetValue(key, out var neighbors)) break;
                bool found = false;
                for (int n = 0; n < neighbors.Count; n++)
                {
                    int idx = neighbors[n];
                    if (used.Contains(idx)) continue;
                    used.Add(idx);
                    if (HexEdgePosKey(segments[idx].a) == key)
                        currentEnd = segments[idx].b;
                    else
                        currentEnd = segments[idx].a;
                    found = true;
                    break;
                }
                if (!found) break;
            }

            var ringObj = new GameObject(ringName);
            ringObj.transform.position = Vector3.zero;
            var lr = ringObj.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.material = MaterialCache.CreateUnlit(color);
            lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                lr.SetPosition(i, points[i]);

            zoneRings.Add(ringObj);
        }

        private static long HexEdgePosKey(Vector3 v)
        {
            int x = Mathf.RoundToInt(v.x * 100);
            int z = Mathf.RoundToInt(v.z * 100);
            return ((long)x << 32) | (uint)z;
        }

        private void CreateSpawnIndicators(Dictionary<HexCoord, HexPieceData> data)
        {
            var spawnPointEdges = new Dictionary<HexCoord, int>();

            foreach (var kvp in data)
            {
                bool hasOpenEdge = false;
                foreach (int edge in kvp.Value.ConnectedEdges)
                {
                    HexCoord neighbor = kvp.Value.Coord.GetNeighbor(edge);
                    if (!data.ContainsKey(neighbor))
                    {
                        hasOpenEdge = true;
                        if (kvp.Value.Type != HexPieceType.Castle)
                            spawnPointEdges[kvp.Key] = edge;
                        break;
                    }
                }
            }

            foreach (var kvp in spawnPointEdges)
            {
                Vector3 edgePos = HexGrid.GetEdgeMidpoint(kvp.Key, kvp.Value);

                var indicator = new GameObject($"SpawnIndicator_{kvp.Key}");
                indicator.transform.position = edgePos + Vector3.up * 5f;

                var sr = indicator.AddComponent<SpriteRenderer>();
                if (goblinSprite != null)
                    sr.sprite = goblinSprite;
                else if (goblinCampSprite != null)
                    sr.sprite = goblinCampSprite;
                sr.color = new Color(1f, 0.3f, 0.1f);

                indicator.AddComponent<BillboardSprite>();
                indicator.AddComponent<SpawnIndicatorPulse>();
                spawnIndicators.Add(indicator);
            }
        }

        private void CreateHiddenSpawnerMarkers()
        {
            foreach (var coord in hiddenSpawners)
            {
                Vector3 worldPos = HexGrid.HexToWorld(coord);

                var marker = new GameObject($"HiddenSpawner_{coord}");
                marker.transform.position = worldPos + Vector3.up * 0.15f;

                // Draw a red hex outline to mark hidden spawner location
                var lr = marker.AddComponent<LineRenderer>();
                lr.loop = true;
                lr.useWorldSpace = true;
                lr.startWidth = 0.5f;
                lr.endWidth = 0.5f;
                lr.positionCount = 6;

                Color spawnerColor = new Color(1f, 0f, 0f, 0.5f);
                lr.material = MaterialCache.CreateUnlit(spawnerColor);

                Vector3[] corners = HexGrid.GetHexCorners(coord);
                for (int i = 0; i < 6; i++)
                    lr.SetPosition(i, new Vector3(corners[i].x, 0.15f, corners[i].z));

                // Skull-like indicator sprite
                if (goblinCampSprite != null)
                {
                    var iconObj = new GameObject("Icon");
                    iconObj.transform.SetParent(marker.transform);
                    iconObj.transform.position = worldPos + Vector3.up * 5f;
                    var sr = iconObj.AddComponent<SpriteRenderer>();
                    sr.sprite = goblinCampSprite;
                    sr.color = new Color(1f, 0.2f, 0.2f, 0.7f);
                    iconObj.AddComponent<BillboardSprite>();
                }

                hiddenSpawnerMarkers.Add(marker);
            }
        }

        private void FitCamera()
        {
            var cam = FindFirstObjectByType<CameraController>();
            if (cam == null) return;

            var positions = new List<Vector3>();
            foreach (var kvp in hexPieces)
                positions.Add(kvp.Value.transform.position);
            foreach (var coord in hiddenSpawners)
                positions.Add(HexGrid.HexToWorld(coord));
            foreach (var kvp in orePatches)
                positions.Add(HexGrid.HexToWorld(kvp.Key));

            if (positions.Count > 0)
                cam.ExpandBoundsToInclude(positions);
        }
    }
}
