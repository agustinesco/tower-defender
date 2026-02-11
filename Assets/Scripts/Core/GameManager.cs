using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Grid;
using TowerDefense.Entities;
using TowerDefense.UI;
using TowerDefense.Data;

namespace TowerDefense.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int startingLives = 30;
        [SerializeField] private int startingCurrency = 200;
        [SerializeField] private int startingTiles = 2;
        [SerializeField] private float buildGracePeriod = 30f;

        [Header("Tower Placement Mode")]
        [SerializeField] private bool useFreeTowerPlacement = true;
        public bool UseFreeTowerPlacement => useFreeTowerPlacement;

        [Header("Materials")]
        [SerializeField] private Material hexBaseMaterial;
        [SerializeField] private Material hexPathMaterial;
        [SerializeField] private Material castleMaterial;

        [Header("Sprites")]
        [SerializeField] private Sprite goblinCampSprite;
        [SerializeField] private Sprite goblinSprite;
        [SerializeField] private Sprite goldSprite;
        [SerializeField] private Sprite oreSprite;
        [SerializeField] private Sprite gemsSprite;
        [SerializeField] private Sprite florpusSprite;
        [SerializeField] private Sprite adamantiteSprite;

        [Header("Prefabs")]
        [SerializeField] private GameObject hexPiecePrefab;
        [SerializeField] private GameObject mineOutpostPrefab;
        [SerializeField] private GameObject groundEnemyPrefab;
        [SerializeField] private GameObject flyingEnemyPrefab;

        [Header("Piece Configs")]
        [SerializeField] private List<HexPieceConfig> pieceConfigs;

        private Dictionary<HexPieceType, HexPieceConfig> pieceConfigLookup;
        private Dictionary<HexCoord, HexPiece> hexPieces = new Dictionary<HexCoord, HexPiece>();
        private Dictionary<HexCoord, HexPieceData> mapData = new Dictionary<HexCoord, HexPieceData>();
        private List<HexCoord> spawnPoints = new List<HexCoord>();
        private Dictionary<HexCoord, int> spawnPointEdges = new Dictionary<HexCoord, int>();
        private MapGenerator mapGenerator;
        private WaveManager waveManager;
        private UpgradeSelectionUI upgradeSelectionUI;
        private TowerManager towerManager;

        private PieceProvider pieceProvider;
        private PlacementValidator placementValidator;
        private GhostPieceManager ghostPieceManager;
        private PieceDragHandler pieceDragHandler;
        private PieceHandUI pieceHandUI;

        private List<GoblinCampSpawner> goblinCampSpawners = new List<GoblinCampSpawner>();
        private HashSet<HexCoord> hiddenSpawners = new HashSet<HexCoord>();

        private Dictionary<HexCoord, OrePatch> orePatches = new Dictionary<HexCoord, OrePatch>();
        private Dictionary<HexCoord, GameObject> orePatchMarkers = new Dictionary<HexCoord, GameObject>();
        private HashSet<HexCoord> activeMiningOutposts = new HashSet<HexCoord>();
        private Dictionary<HexCoord, GameObject> mineVisuals = new Dictionary<HexCoord, GameObject>();
        private Dictionary<HexCoord, UI.MineTimerIndicator> mineTimerIndicators = new Dictionary<HexCoord, UI.MineTimerIndicator>();

        private const int MineCost = 100;
        private const int LureCost = 75;
        private const float LureGoldMultiplier = 2f;
        private const int HasteCost = 400;
        private const int GoldenTouchCost = 500;
        private const float ModContinuousDuration = 120f; // 2 minutes

        // Zone system — each entry is the max hex distance for that zone boundary
        [Header("Zone Boundaries (hex distances)")]
        [SerializeField] private int[] zoneBoundaries = { 3, 6, 9 };
        private HashSet<int> unlockedZones = new HashSet<int> { 1 };
        private HashSet<int> pendingBossZones = new HashSet<int>();
        private List<GameObject> zoneRings = new List<GameObject>();

        private List<GameObject> spawnIndicators = new List<GameObject>();

        private HashSet<HexCoord> activeLures = new HashSet<HexCoord>();
        private Dictionary<HexCoord, GameObject> lureVisuals = new Dictionary<HexCoord, GameObject>();

        private Dictionary<HexCoord, float> activeHastes = new Dictionary<HexCoord, float>();
        private Dictionary<HexCoord, GameObject> hasteVisuals = new Dictionary<HexCoord, GameObject>();
        private Dictionary<HexCoord, float> activeGoldenTouches = new Dictionary<HexCoord, float>();
        private Dictionary<HexCoord, GameObject> goldenTouchVisuals = new Dictionary<HexCoord, GameObject>();

        private int currentLives;
        private int maxLives;
        private int currentCurrency;
        private int currentWave = 0;
        private bool gameOver = false;
        private bool buildPhaseActive = false;
        private float buildTimer = 0f;

        private const float ContinuousMineInterval = 30f;
        private float continuousMineTimer = 0f;

        public int Lives => currentLives;
        public int MaxLives => maxLives;
        public int Currency => currentCurrency;
        public int Wave => currentWave;
        public bool IsGameOver => gameOver;
        public Dictionary<HexCoord, HexPiece> HexPieces => hexPieces;
        public List<HexCoord> SpawnPoints => spawnPoints;
        public Dictionary<HexCoord, HexPieceData> MapData => mapData;
        public IReadOnlyList<GoblinCampSpawner> GoblinCampSpawners => goblinCampSpawners;
        public IReadOnlyCollection<HexCoord> ActiveMiningOutposts => activeMiningOutposts;
        public IReadOnlyCollection<HexCoord> ActiveLures => activeLures;
        public float LureGoldMult => LureGoldMultiplier;
        public bool HasPendingBoss => pendingBossZones.Count > 0;
        public IReadOnlyCollection<int> PendingBossZones => pendingBossZones;

        public bool IsContinuousMode => GameModeSelection.SelectedMode == Data.GameMode.Continuous;
        public bool BuildPhaseActive => buildPhaseActive;
        public float BuildTimer => buildTimer;
        public Sprite GoblinCampSprite => goblinCampSprite;
        public Sprite GoldSprite => goldSprite;
        public GameObject GroundEnemyPrefab => groundEnemyPrefab;
        public GameObject FlyingEnemyPrefab => flyingEnemyPrefab;
        public IReadOnlyCollection<HexCoord> HiddenSpawners => hiddenSpawners;
        public IReadOnlyDictionary<HexCoord, int> SpawnPointEdges => spawnPointEdges;

        public Sprite GetResourceSprite(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Gems: return gemsSprite;
                case ResourceType.Florpus: return florpusSprite;
                case ResourceType.Adamantite: return adamantiteSprite != null ? adamantiteSprite : oreSprite;
                default: return oreSprite;
            }
        }

        public Color GetResourceColor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.IronOre: return new Color(0.7f, 0.7f, 0.75f);
                case ResourceType.Gems: return new Color(0.5f, 0.2f, 1f);
                case ResourceType.Florpus: return new Color(0.2f, 0.9f, 0.5f);
                case ResourceType.Adamantite: return new Color(1f, 0.3f, 0.3f);
                default: return Color.white;
            }
        }

        public event System.Action<int> OnLivesChanged;
        public event System.Action<int> OnCurrencyChanged;
        public event System.Action<int> OnWaveChanged;
        public event System.Action OnGameOver;
        public event System.Action OnBuildPhaseStarted;
        public event System.Action OnBuildPhaseEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Create persistent managers early so they exist before any Start() calls
            if (PersistenceManager.Instance == null)
            {
                var persistObj = new GameObject("PersistenceManager");
                persistObj.AddComponent<PersistenceManager>();
            }
            if (LabManager.Instance == null)
            {
                var labObj = new GameObject("LabManager");
                labObj.AddComponent<LabManager>();
            }

            // Initialize currency/lives in Awake so other scripts' Start() reads correct values
            currentLives = startingLives + (LabManager.Instance != null ? LabManager.Instance.BonusStartingLives + LabManager.Instance.BonusMaxHP : 0);
            maxLives = currentLives;
            currentCurrency = startingCurrency + (LabManager.Instance != null ? LabManager.Instance.BonusStartingGold : 0);
        }

        private void Start()
        {
            try
            {
                InitializeGame();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GameManager.Start FAILED: {e.Message}\n{e.StackTrace}");
            }
        }

        private void InitializeGame()
        {
            // Build piece config lookup
            pieceConfigLookup = new Dictionary<HexPieceType, HexPieceConfig>();
            if (pieceConfigs != null)
            {
                foreach (var config in pieceConfigs)
                {
                    pieceConfigLookup[config.pieceType] = config;
                }
            }

            // 0. Create EnemyManager (per-scene, not DontDestroyOnLoad)
            if (EnemyManager.Instance == null)
            {
                var enemyMgrObj = new GameObject("EnemyManager");
                enemyMgrObj.AddComponent<EnemyManager>();
            }

            // 1. Generate castle and starting path
            mapGenerator = new MapGenerator();
            mapData = mapGenerator.GenerateInitialCastle();

            // 1a. Auto-generate starting tiles
            var startingPieces = mapGenerator.GenerateStartingPath(startingTiles);
            foreach (var piece in startingPieces)
            {
                mapData[piece.Coord] = piece;
            }

            // Create all initial pieces visually
            foreach (var kvp in mapData)
            {
                CreateHexPiece(kvp.Value);
            }

            // 1b. Generate hidden spawner positions (goblin camps disabled)
            hiddenSpawners = new HashSet<HexCoord>();

            // 1c. Generate ore patches and create visual markers
            int zone1Boundary = zoneBoundaries.Length > 0 ? zoneBoundaries[0] : 3;
            orePatches = mapGenerator.GenerateOrePatches(minDistance: 2, maxDistance: 6, zoneBoundary: zone1Boundary);
            CreateOrePatchMarkers();

            // 1e. Create zone boundary visuals
            CreateZoneRings();

            // 2. Initialize piece provider
            pieceProvider = new PieceProvider(pieceConfigs);
            pieceProvider.Initialize();
            pieceProvider.OnHandChanged += OnHandChanged;

            // 3. Initialize placement validator
            placementValidator = new PlacementValidator(mapData, pieceConfigLookup);
            UpdateNonReplaceableCoords();

            // 3b. Detect open-edge spawn points
            UpdateSpawnPoints();
            CreateSpawnIndicators();

            // 4. Initialize ghost piece manager
            GameObject ghostManagerObj = new GameObject("GhostPieceManager");
            ghostPieceManager = ghostManagerObj.AddComponent<GhostPieceManager>();
            ghostPieceManager.Initialize(goblinCampSprite);
            ghostPieceManager.SetHiddenSpawners(hiddenSpawners);

            // 5. Initialize drag handler
            GameObject dragHandlerObj = new GameObject("PieceDragHandler");
            pieceDragHandler = dragHandlerObj.AddComponent<PieceDragHandler>();

            // 6. Fit camera to castle
            var cameraController = FindFirstObjectByType<CameraController>();
            if (cameraController != null)
            {
                var positions = new List<Vector3>();
                foreach (var kvp in hexPieces)
                {
                    positions.Add(kvp.Value.transform.position);
                }
                if (positions.Count > 0)
                {
                    cameraController.FitToPositions(positions);
                }
                cameraController.pieceDragHandler = pieceDragHandler;
            }

            // 7. Subscribe to wave events
            waveManager = FindFirstObjectByType<WaveManager>();
            if (waveManager != null)
            {
                waveManager.OnWaveComplete += OnWaveComplete;
            }

            upgradeSelectionUI = FindFirstObjectByType<UpgradeSelectionUI>();
            if (upgradeSelectionUI != null)
            {
                upgradeSelectionUI.OnNextWave += OnShopNextWave;
                upgradeSelectionUI.OnExitRun += OnShopExitRun;
            }

            // 8. Wire placement event
            pieceDragHandler.OnPiecePlaced += OnPiecePlaced;

            // 8b. Wire PieceDragHandler to TowerManager so tower placement is blocked during piece placement
            towerManager = FindFirstObjectByType<TowerManager>();
            if (towerManager != null)
            {
                towerManager.SetPieceDragHandler(pieceDragHandler);
            }

            // 9. Find PieceHandUI in scene (must exist as prefab instance)
            pieceHandUI = FindFirstObjectByType<PieceHandUI>();
            if (pieceHandUI == null)
            {
                Debug.LogError("PieceHandUI not found in scene! Ensure PieceHandUI_Canvas prefab is in the scene.");
            }
            if (pieceHandUI != null)
            {
                pieceHandUI.SetPieceConfigs(pieceConfigLookup);
                pieceHandUI.SetPieceProvider(pieceProvider);

                // Set free tower mode BEFORE first RefreshHand so tabs are created correctly
                if (useFreeTowerPlacement && towerManager != null)
                {
                    pieceHandUI.SetFreeTowerMode(true);
                    pieceHandUI.SetAvailableTowers(towerManager.AllTowers);
                }

                pieceDragHandler.Initialize(placementValidator, ghostPieceManager, pieceHandUI);
                pieceHandUI.RefreshHand(pieceProvider.Pieces);
            }

            Invoke(nameof(FireInitialEvents), 0.1f);
        }

        private void Update()
        {
            if (gameOver) return;
            if (pieceProvider == null) return;

            pieceProvider.UpdateCooldowns(Time.deltaTime);

            if (buildPhaseActive)
            {
                buildTimer -= Time.deltaTime;
                if (buildTimer <= 0f)
                {
                    EndBuildPhase();
                }
            }

            if (IsContinuousMode)
                UpdateModificationTimers(Time.deltaTime);

            if (IsContinuousMode && activeMiningOutposts.Count > 0)
            {
                continuousMineTimer += Time.deltaTime;
                if (continuousMineTimer >= ContinuousMineInterval)
                {
                    continuousMineTimer = 0f;
                    CollectMiningResources();

                    // Reset all timer indicators
                    foreach (var kvp in mineTimerIndicators)
                    {
                        if (kvp.Value != null)
                            kvp.Value.SetTimer(0f);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (waveManager != null)
                waveManager.OnWaveComplete -= OnWaveComplete;

            if (upgradeSelectionUI != null)
            {
                upgradeSelectionUI.OnNextWave -= OnShopNextWave;
                upgradeSelectionUI.OnExitRun -= OnShopExitRun;
            }

            if (pieceDragHandler != null)
                pieceDragHandler.OnPiecePlaced -= OnPiecePlaced;

            if (pieceProvider != null)
                pieceProvider.OnHandChanged -= OnHandChanged;
        }

        private void FireInitialEvents()
        {
            OnLivesChanged?.Invoke(currentLives);
            OnCurrencyChanged?.Invoke(currentCurrency);
            OnWaveChanged?.Invoke(currentWave);

            // Refresh hand UI after everything is wired
            if (pieceHandUI != null)
            {
                pieceHandUI.RefreshHand(pieceProvider.Pieces);
            }
        }

        private void OnHandChanged()
        {
            if (pieceHandUI != null)
            {
                pieceHandUI.RefreshHand(pieceProvider.Pieces);
            }
        }

        private void OnPiecePlaced(int handIndex, PlacementRotation rotation, HexCoord coord)
        {
            var config = pieceProvider.GetConfig(handIndex);
            HexPieceType type = config.pieceType;

            // Check and spend gold
            if (!SpendCurrency(config.placementCost))
                return;

            // Handle replacement of existing piece
            if (hexPieces.TryGetValue(coord, out var existingPiece))
            {
                ReplacePiece(coord, existingPiece);
            }

            // Create piece data and register in map
            var pieceData = mapGenerator.PlacePiece(coord, type, rotation.ConnectedEdges);
            mapData[coord] = pieceData;

            // Create the visual piece
            CreateHexPiece(pieceData);

            // Start cooldown on the piece card
            pieceProvider.StartCooldown(handIndex);

            // Update placement validator with new map
            placementValidator.UpdateMap(mapData);
            UpdateNonReplaceableCoords();

            // Update spawn points
            UpdateSpawnPoints();
            CreateSpawnIndicators();

            // Recalculate paths for wave manager
            if (waveManager != null)
            {
                waveManager.RecalculatePaths();
            }

            // If this piece is a spawner, add a GoblinCampSpawner
            TryCreateSpawner(coord, type);

            // Check if placing this piece reveals any hidden spawners
            RevealAdjacentSpawners(coord, rotation.ConnectedEdges);

            // Recalculate all goblin camp paths (map topology may have changed)
            RecalculateGoblinCampPaths();

            // Expand camera bounds to include new piece
            var cameraController = FindFirstObjectByType<CameraController>();
            if (cameraController != null)
            {
                var newPositions = new List<Vector3> { hexPieces[coord].transform.position };
                cameraController.ExpandBoundsToInclude(newPositions);
            }

            // Check if piece enters a new zone
            int zone = GetZone(coord);
            if (zone > 1 && !unlockedZones.Contains(zone) && !pendingBossZones.Contains(zone))
            {
                pendingBossZones.Add(zone);
                Debug.Log($"Entering zone {zone}! Boss will spawn next wave.");
            }

            // Auto-build mine on ore deposits
            if (orePatches.ContainsKey(coord) && !activeMiningOutposts.Contains(coord))
                AutoBuildMine(coord);

            Debug.Log($"Piece placed at {coord}: {type} with edges [{string.Join(", ", rotation.ConnectedEdges)}]. Spawn points: {spawnPoints.Count}");
        }

        private void AutoBuildMine(HexCoord coord)
        {
            if (!orePatches.ContainsKey(coord)) return;
            if (activeMiningOutposts.Contains(coord)) return;
            if (!mapData.ContainsKey(coord)) return;

            activeMiningOutposts.Add(coord);
            UpdateNonReplaceableCoords();

            if (hexPieces.TryGetValue(coord, out var hex))
            {
                var visual = CreateMineVisual(hex.transform);
                mineVisuals[coord] = visual;
            }

            RemoveOrePatchMarker(coord);

            if (IsContinuousMode)
            {
                Vector3 worldPos = HexGrid.HexToWorld(coord);
                var indicatorObj = new GameObject("MineTimer");
                indicatorObj.transform.position = worldPos + Vector3.up * 10f;
                var indicator = indicatorObj.AddComponent<UI.MineTimerIndicator>();
                indicator.Initialize(ContinuousMineInterval);
                indicator.SetTimer(continuousMineTimer);
                mineTimerIndicators[coord] = indicator;
            }

            var patch = orePatches[coord];
            Debug.Log($"Auto-mine built at {coord}: {patch.ResourceType} (yield: {patch.BaseYield}/wave)");
        }

        private void ReplacePiece(HexCoord coord, HexPiece existingPiece)
        {
            // Refund gold for any towers on the tile
            if (towerManager != null)
            {
                towerManager.ClearSelection();
                int refund = towerManager.RemoveTowersOnTile(coord);
                if (refund > 0)
                {
                    AddCurrency(refund);
                    Debug.Log($"Refunded {refund}g from towers on replaced piece at {coord}");
                }
            }

            // Destroy the old visual piece (takes tower slots with it)
            Destroy(existingPiece.gameObject);
            hexPieces.Remove(coord);
        }

        private void UpdateNonReplaceableCoords()
        {
            var nonReplaceable = new HashSet<HexCoord>();

            foreach (var kvp in mapData)
            {
                var piece = kvp.Value;
                // Castle and GoblinCamp are never replaceable
                if (piece.IsCastle || piece.IsGoblinCamp)
                {
                    nonReplaceable.Add(kvp.Key);
                    continue;
                }
                // Tiles with active mines
                if (activeMiningOutposts.Contains(kvp.Key))
                {
                    nonReplaceable.Add(kvp.Key);
                    continue;
                }
                // Tiles with active lures
                if (activeLures.Contains(kvp.Key))
                {
                    nonReplaceable.Add(kvp.Key);
                    continue;
                }
                // Tiles with active haste
                if (activeHastes.ContainsKey(kvp.Key))
                {
                    nonReplaceable.Add(kvp.Key);
                    continue;
                }
                // Tiles with active golden touch
                if (activeGoldenTouches.ContainsKey(kvp.Key))
                {
                    nonReplaceable.Add(kvp.Key);
                    continue;
                }
            }

            placementValidator.SetNonReplaceableCoords(nonReplaceable);
        }

        private void TryCreateSpawner(HexCoord coord, HexPieceType type)
        {
            HexPieceConfig placedConfig = null;
            pieceConfigLookup.TryGetValue(type, out placedConfig);
            bool isSpawner = placedConfig != null ? placedConfig.isSpawner : type == HexPieceType.GoblinCamp;

            if (!isSpawner) return;

            var pathFinder = new PathFinder(mapData);
            var path = pathFinder.FindPathToCastle(coord);
            if (path.Count > 0)
            {
                float healthMul = placedConfig != null ? placedConfig.spawnerHealthMultiplier : 0.7f;
                float speedMul = placedConfig != null ? placedConfig.spawnerSpeedMultiplier : 0.85f;

                var spawner = hexPieces[coord].gameObject.AddComponent<GoblinCampSpawner>();
                spawner.Initialize(coord, path, healthMul, speedMul);
                goblinCampSpawners.Add(spawner);
            }
        }

        private void RevealAdjacentSpawners(HexCoord placedCoord, List<int> connectedEdges)
        {
            foreach (int edge in connectedEdges)
            {
                HexCoord neighborCoord = placedCoord.GetNeighbor(edge);

                if (!hiddenSpawners.Contains(neighborCoord)) continue;
                if (mapData.ContainsKey(neighborCoord)) continue;

                // Reveal this spawner: create a GoblinCamp piece connecting back
                int entryEdge = HexCoord.OppositeEdge(edge);
                int exitEdge = HexCoord.OppositeEdge(entryEdge);
                var spawnerEdges = new List<int> { entryEdge, exitEdge };

                var spawnerData = mapGenerator.PlacePiece(neighborCoord, HexPieceType.GoblinCamp, spawnerEdges);
                mapData[neighborCoord] = spawnerData;
                CreateHexPiece(spawnerData);

                // Remove from hidden set
                hiddenSpawners.Remove(neighborCoord);
                mapGenerator.RemoveHiddenSpawner(neighborCoord);
                ghostPieceManager.SetHiddenSpawners(hiddenSpawners);

                // Wire spawner logic
                TryCreateSpawner(neighborCoord, HexPieceType.GoblinCamp);

                // Update placement validator
                placementValidator.UpdateMap(mapData);
                UpdateNonReplaceableCoords();

                // Update spawn points
                UpdateSpawnPoints();
                CreateSpawnIndicators();

                // Expand camera
                var cameraController = FindFirstObjectByType<CameraController>();
                if (cameraController != null)
                {
                    var pos = new List<Vector3> { hexPieces[neighborCoord].transform.position };
                    cameraController.ExpandBoundsToInclude(pos);
                }

                Debug.Log($"Revealed hidden spawner at {neighborCoord}!");
            }
        }

        private void UpdateSpawnPoints()
        {
            spawnPoints.Clear();
            spawnPointEdges.Clear();
            foreach (var kvp in mapData)
            {
                if (kvp.Value.IsCastle) continue;
                // Goblin camps burst-spawn via their own spawner, not wave spawn points
                if (kvp.Value.Type == HexPieceType.GoblinCamp) continue;

                // Config-based wave spawn points
                if (pieceConfigLookup.TryGetValue(kvp.Value.Type, out var spConfig))
                {
                    if (spConfig.isWaveSpawnPoint)
                    {
                        spawnPoints.Add(kvp.Key);
                        StoreOpenEdge(kvp.Key, kvp.Value);
                        continue;
                    }
                }
                else if (kvp.Value.IsSpawnPoint)
                {
                    spawnPoints.Add(kvp.Key);
                    StoreOpenEdge(kvp.Key, kvp.Value);
                    continue;
                }

                // Pieces with open edges become wave spawn points
                foreach (int edge in kvp.Value.ConnectedEdges)
                {
                    HexCoord neighbor = kvp.Value.Coord.GetNeighbor(edge);
                    if (!mapData.ContainsKey(neighbor))
                    {
                        spawnPoints.Add(kvp.Key);
                        spawnPointEdges[kvp.Key] = edge;
                        break;
                    }
                }
            }
        }

        private void StoreOpenEdge(HexCoord coord, HexPieceData data)
        {
            foreach (int edge in data.ConnectedEdges)
            {
                HexCoord neighbor = coord.GetNeighbor(edge);
                if (!mapData.ContainsKey(neighbor))
                {
                    spawnPointEdges[coord] = edge;
                    return;
                }
            }
        }

        private void CreateSpawnIndicators()
        {
            // Destroy old indicators
            foreach (var ind in spawnIndicators)
            {
                if (ind != null) Destroy(ind);
            }
            spawnIndicators.Clear();

            foreach (var kvp in spawnPointEdges)
            {
                HexCoord coord = kvp.Key;
                int edge = kvp.Value;
                Vector3 edgePos = HexGrid.GetEdgeMidpoint(coord, edge);

                var indicator = new GameObject($"SpawnIndicator_{coord}");
                indicator.transform.position = edgePos + Vector3.up * 5f;

                // Goblin sprite billboard
                var sr = indicator.AddComponent<SpriteRenderer>();
                if (goblinSprite != null)
                    sr.sprite = goblinSprite;
                else if (goblinCampSprite != null)
                    sr.sprite = goblinCampSprite;
                sr.color = new Color(1f, 0.3f, 0.1f);

                indicator.AddComponent<UI.BillboardSprite>();
                indicator.AddComponent<UI.SpawnIndicatorPulse>();
                spawnIndicators.Add(indicator);
            }
        }

        public void SetSpawnIndicatorsVisible(bool visible)
        {
            foreach (var ind in spawnIndicators)
            {
                if (ind != null) ind.SetActive(visible);
            }
        }

        private void CreateHexPiece(HexPieceData data)
        {
            GameObject pieceObj;

            if (hexPiecePrefab != null)
            {
                pieceObj = Instantiate(hexPiecePrefab);
            }
            else
            {
                pieceObj = new GameObject($"HexPiece_{data.Coord}");
            }

            var piece = pieceObj.GetComponent<HexPiece>();
            if (piece == null)
            {
                piece = pieceObj.AddComponent<HexPiece>();
            }

            HexPieceConfig config = null;
            pieceConfigLookup?.TryGetValue(data.Type, out config);

            piece.Initialize(data, config, hexBaseMaterial, hexPathMaterial, castleMaterial);
            hexPieces[data.Coord] = piece;
        }

        public void AddCurrency(int amount)
        {
            currentCurrency += amount;
            OnCurrencyChanged?.Invoke(currentCurrency);
        }

        public bool SpendCurrency(int amount)
        {
            if (currentCurrency < amount) return false;
            currentCurrency -= amount;
            OnCurrencyChanged?.Invoke(currentCurrency);
            return true;
        }

        public void LoseLife()
        {
            currentLives--;
            OnLivesChanged?.Invoke(currentLives);

            if (currentLives <= 0)
            {
                gameOver = true;
                // All gathered resources are lost on death
                PersistenceManager.Instance?.ResetRun();
                UpgradeManager.Instance?.ResetForNewRun();
                OnGameOver?.Invoke();
            }
        }

        public void ExitRun()
        {
            if (gameOver) return;
            gameOver = true;
            // Bank all gathered resources on voluntary exit
            PersistenceManager.Instance?.BankRunResources();
            UpgradeManager.Instance?.ResetForNewRun();
            OnGameOver?.Invoke();
            Debug.Log("Player exited run. All gathered resources banked.");
        }

        public void StartNextWave()
        {
            if (gameOver) return;
            currentWave++;
            OnWaveChanged?.Invoke(currentWave);
            if (!IsContinuousMode)
            {
                SetSpawnIndicatorsVisible(false);
            }
        }

        private void OnWaveComplete()
        {
            Debug.Log("Wave complete! Recalculating spawn points and paths...");

            // Unlock boss zones if the wave with boss(es) was cleared
            if (pendingBossZones.Count > 0)
            {
                UnlockPendingBossZones();
            }

            UpdateSpawnPoints();
            CreateSpawnIndicators();
            SetSpawnIndicatorsVisible(true);
            RecalculateGoblinCampPaths();
            if (waveManager != null)
            {
                waveManager.RecalculatePaths();
            }
            CollectMiningResources();
            if (!IsContinuousMode)
            {
                ExpireWaveModifications();
                ShowUpgradeSelection();
            }
        }

        private void ShowUpgradeSelection()
        {
            if (upgradeSelectionUI != null)
            {
                upgradeSelectionUI.Show();
            }
        }

        private void OnShopNextWave()
        {
            StartBuildPhase();
        }

        private void StartBuildPhase()
        {
            buildPhaseActive = true;
            buildTimer = buildGracePeriod;
            SetSpawnIndicatorsVisible(true);
            OnBuildPhaseStarted?.Invoke();
        }

        public void SkipBuildPhase()
        {
            if (buildPhaseActive)
                EndBuildPhase();
        }

        private void EndBuildPhase()
        {
            buildPhaseActive = false;
            buildTimer = 0f;
            OnBuildPhaseEnded?.Invoke();
            if (waveManager != null)
                waveManager.StartWave();
        }

        private void OnShopExitRun()
        {
            ExitRun();
        }

        private void RecalculateGoblinCampPaths()
        {
            var pathFinder = new PathFinder(mapData);
            foreach (var spawner in goblinCampSpawners)
            {
                var path = pathFinder.FindPathToCastle(spawner.Coord);
                spawner.UpdatePath(path);
            }
        }

        public HexCoord? GetNearestOreDeposit()
        {
            HexCoord castle = new HexCoord(0, 0);
            HexCoord? nearest = null;
            int bestDist = int.MaxValue;
            foreach (var coord in orePatches.Keys)
            {
                int dist = castle.DistanceTo(coord);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = coord;
                }
            }
            return nearest;
        }

        public HexCoord? GetGuaranteedOreDeposit()
        {
            return mapGenerator?.GuaranteedOreCoord;
        }

        public OrePatch? GetOrePatchAt(HexCoord coord)
        {
            if (orePatches.TryGetValue(coord, out var patch))
                return patch;
            return null;
        }

        public bool HasMine(HexCoord coord)
        {
            return activeMiningOutposts.Contains(coord);
        }

        public int GetMineCost()
        {
            return MineCost;
        }

        public bool BuildMine(HexCoord coord)
        {
            if (!orePatches.ContainsKey(coord)) return false;
            if (activeMiningOutposts.Contains(coord)) return false;
            if (!mapData.ContainsKey(coord)) return false;
            if (!SpendCurrency(MineCost)) return false;

            activeMiningOutposts.Add(coord);
            UpdateNonReplaceableCoords();

            // Create mine visual on the hex
            if (hexPieces.TryGetValue(coord, out var hex))
            {
                var visual = CreateMineVisual(hex.transform);
                mineVisuals[coord] = visual;
            }

            // Remove the ore patch marker
            RemoveOrePatchMarker(coord);

            // In continuous mode, add a timer indicator above the mine
            if (IsContinuousMode)
            {
                Vector3 worldPos = HexGrid.HexToWorld(coord);
                var indicatorObj = new GameObject("MineTimer");
                indicatorObj.transform.position = worldPos + Vector3.up * 10f;
                var indicator = indicatorObj.AddComponent<UI.MineTimerIndicator>();
                indicator.Initialize(ContinuousMineInterval);
                indicator.SetTimer(continuousMineTimer);
                mineTimerIndicators[coord] = indicator;
            }

            var patch = orePatches[coord];
            Debug.Log($"Mine built at {coord}: {patch.ResourceType} (yield: {patch.BaseYield}/wave)");
            return true;
        }

        private GameObject CreateMineVisual(Transform parent)
        {
            GameObject mine = Instantiate(mineOutpostPrefab, parent);
            mine.transform.localPosition = new Vector3(0f, 5f, 0f);
            mine.transform.localScale = Vector3.one;
            return mine;
        }

        public bool HasLure(HexCoord coord)
        {
            return activeLures.Contains(coord);
        }

        public int GetLureCost()
        {
            return LureCost;
        }

        public bool BuildLure(HexCoord coord)
        {
            if (activeLures.Contains(coord)) return false;
            if (!mapData.ContainsKey(coord)) return false;
            if (mapData[coord].IsCastle) return false;
            if (!SpendCurrency(LureCost)) return false;

            activeLures.Add(coord);
            UpdateNonReplaceableCoords();

            // Create lure visual on the hex
            if (hexPieces.TryGetValue(coord, out var hex))
            {
                var lureVisual = CreateLureVisual(hex.transform);
                lureVisuals[coord] = lureVisual;
            }

            Debug.Log($"Lure placed at {coord}. Enemies will spawn here next wave.");
            return true;
        }

        public List<HexCoord> ConsumeAndGetLures()
        {
            var lures = new List<HexCoord>(activeLures);
            activeLures.Clear();

            // Remove lure visuals
            foreach (var kvp in lureVisuals)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            lureVisuals.Clear();

            return lures;
        }

        private GameObject CreateLureVisual(Transform parent)
        {
            GameObject lure = new GameObject("Lure");
            lure.transform.SetParent(parent);
            lure.transform.localPosition = Vector3.zero;

            // Glowing beacon
            GameObject beacon = MaterialCache.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "LureBeacon";
            beacon.transform.SetParent(lure.transform);
            beacon.transform.localPosition = new Vector3(0f, 4f, 0f);
            beacon.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            var br = beacon.GetComponent<Renderer>();
            if (br != null)
            {
                br.material = MaterialCache.CreateUnlit(new Color(1f, 0.85f, 0.2f));
            }

            // Pole
            GameObject pole = MaterialCache.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "LurePole";
            pole.transform.SetParent(lure.transform);
            pole.transform.localPosition = new Vector3(0f, 2f, 0f);
            pole.transform.localScale = new Vector3(0.3f, 2f, 0.3f);
            var pr = pole.GetComponent<Renderer>();
            if (pr != null)
            {
                pr.material = MaterialCache.CreateUnlit(new Color(0.6f, 0.5f, 0.1f));
            }

            return lure;
        }

        // --- Haste & Golden Touch ---

        public bool HasHaste(HexCoord coord) => activeHastes.ContainsKey(coord);
        public bool HasGoldenTouch(HexCoord coord) => activeGoldenTouches.ContainsKey(coord);
        public int GetHasteCost() => HasteCost;
        public int GetGoldenTouchCost() => GoldenTouchCost;

        public bool TileHasTower(HexCoord coord)
        {
            if (towerManager != null)
            {
                var towers = towerManager.GetTowersOnTile(coord);
                if (towers != null && towers.Count > 0) return true;
            }
            return false;
        }

        public bool BuildHaste(HexCoord coord)
        {
            if (activeHastes.ContainsKey(coord)) return false;
            if (!mapData.ContainsKey(coord)) return false;
            if (!SpendCurrency(HasteCost)) return false;

            activeHastes[coord] = ModContinuousDuration;

            if (hexPieces.TryGetValue(coord, out var hex))
            {
                var visual = CreateModRingVisual(hex.transform, new Color(1f, 0.5f, 0.1f, 0.6f));
                hasteVisuals[coord] = visual;
            }

            SetTowerHaste(coord, 1.3f);
            Debug.Log($"Haste applied at {coord} — towers fire 30% faster.");
            return true;
        }

        public bool BuildGoldenTouch(HexCoord coord)
        {
            if (activeGoldenTouches.ContainsKey(coord)) return false;
            if (!mapData.ContainsKey(coord)) return false;
            if (!SpendCurrency(GoldenTouchCost)) return false;

            activeGoldenTouches[coord] = ModContinuousDuration;

            if (hexPieces.TryGetValue(coord, out var hex))
            {
                var visual = CreateModRingVisual(hex.transform, new Color(1f, 0.85f, 0.1f, 0.6f));
                goldenTouchVisuals[coord] = visual;
            }

            Debug.Log($"Golden Touch applied at {coord} — enemies killed here drop 1.5x gold.");
            return true;
        }

        public float GetGoldenTouchMultiplierAt(Vector3 worldPos)
        {
            foreach (var coord in activeGoldenTouches.Keys)
            {
                Vector3 hexCenter = HexGrid.HexToWorld(coord);
                float dx = worldPos.x - hexCenter.x;
                float dz = worldPos.z - hexCenter.z;
                if (dx * dx + dz * dz <= HexGrid.OuterRadius * HexGrid.OuterRadius)
                    return 1.5f;
            }
            return 1f;
        }

        private GameObject CreateModRingVisual(Transform parent, Color color)
        {
            var ring = MaterialCache.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "ModRing";
            ring.transform.SetParent(parent);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(HexGrid.OuterRadius * 1.6f, 0.01f, HexGrid.OuterRadius * 1.6f);

            var rend = ring.GetComponent<Renderer>();
            if (rend != null)
                rend.material = MaterialCache.CreateUnlit(color);

            return ring;
        }

        // Reusable lists to avoid per-frame allocations in UpdateModificationTimers
        private readonly List<HexCoord> _expiredModCoords = new List<HexCoord>();
        private readonly List<HexCoord> _modKeySnapshot = new List<HexCoord>();

        private void UpdateModificationTimers(float dt)
        {
            // Tick haste timers
            _expiredModCoords.Clear();
            _modKeySnapshot.Clear();
            _modKeySnapshot.AddRange(activeHastes.Keys);
            for (int i = 0; i < _modKeySnapshot.Count; i++)
            {
                var coord = _modKeySnapshot[i];
                float t = activeHastes[coord] - dt;
                if (t <= 0f)
                    _expiredModCoords.Add(coord);
                else
                    activeHastes[coord] = t;
            }
            for (int i = 0; i < _expiredModCoords.Count; i++)
                RemoveHaste(_expiredModCoords[i]);

            // Tick golden touch timers
            _expiredModCoords.Clear();
            _modKeySnapshot.Clear();
            _modKeySnapshot.AddRange(activeGoldenTouches.Keys);
            for (int i = 0; i < _modKeySnapshot.Count; i++)
            {
                var coord = _modKeySnapshot[i];
                float t = activeGoldenTouches[coord] - dt;
                if (t <= 0f)
                    _expiredModCoords.Add(coord);
                else
                    activeGoldenTouches[coord] = t;
            }
            for (int i = 0; i < _expiredModCoords.Count; i++)
                RemoveGoldenTouch(_expiredModCoords[i]);
        }

        private void ExpireWaveModifications()
        {
            var hasteCoords = new List<HexCoord>(activeHastes.Keys);
            foreach (var coord in hasteCoords)
                RemoveHaste(coord);

            var gtCoords = new List<HexCoord>(activeGoldenTouches.Keys);
            foreach (var coord in gtCoords)
                RemoveGoldenTouch(coord);
        }

        private void RemoveHaste(HexCoord coord)
        {
            activeHastes.Remove(coord);
            if (hasteVisuals.TryGetValue(coord, out var vis))
            {
                if (vis != null) Destroy(vis);
                hasteVisuals.Remove(coord);
            }
            SetTowerHaste(coord, 1f);
        }

        private void SetTowerHaste(HexCoord coord, float multiplier)
        {
            if (towerManager == null) return;
            var towers = towerManager.GetTowersOnTile(coord);
            if (towers == null) return;
            for (int i = 0; i < towers.Count; i++)
            {
                if (towers[i] != null)
                    towers[i].SetHasteMultiplier(multiplier);
            }
        }

        private void RemoveGoldenTouch(HexCoord coord)
        {
            activeGoldenTouches.Remove(coord);
            if (goldenTouchVisuals.TryGetValue(coord, out var vis))
            {
                if (vis != null) Destroy(vis);
                goldenTouchVisuals.Remove(coord);
            }
        }

        // --- Zone System ---

        public int GetZone(HexCoord coord)
        {
            int dist = new HexCoord(0, 0).DistanceTo(coord);
            for (int i = 0; i < zoneBoundaries.Length; i++)
            {
                if (dist <= zoneBoundaries[i])
                    return i + 1;
            }
            return zoneBoundaries.Length + 1;
        }

        public bool IsZoneUnlocked(int zone)
        {
            return unlockedZones.Contains(zone);
        }

        public void UnlockPendingBossZones()
        {
            foreach (var zone in pendingBossZones)
            {
                unlockedZones.Add(zone);
                Debug.Log($"Zone {zone} unlocked!");
            }
            pendingBossZones.Clear();
        }

        public float GetZoneHealthMultiplier(int zone)
        {
            return 1f + (zone - 1) * 0.5f;
        }

        public float GetZoneSpeedMultiplier(int zone)
        {
            return 1f + (zone - 1) * 0.1f;
        }

        private void CreateZoneRings()
        {
            Color[] ringColors = new Color[]
            {
                new Color(0.8f, 0.8f, 0.2f, 0.6f),  // yellow
                new Color(0.8f, 0.5f, 0.2f, 0.6f),  // orange
                new Color(0.8f, 0.2f, 0.2f, 0.6f),  // red
                new Color(0.6f, 0.1f, 0.1f, 0.6f),  // dark red
            };

            for (int i = 0; i < zoneBoundaries.Length; i++)
            {
                float worldRadius = 2f * HexGrid.InnerRadius * zoneBoundaries[i];
                Color color = ringColors[Mathf.Min(i, ringColors.Length - 1)];

                var ringObj = new GameObject($"ZoneRing_{i + 2}");
                ringObj.transform.position = Vector3.zero;

                var lr = ringObj.AddComponent<LineRenderer>();
                lr.loop = true;
                lr.useWorldSpace = true;
                lr.startWidth = 1.5f;
                lr.endWidth = 1.5f;
                lr.positionCount = 60;

                lr.material = MaterialCache.CreateUnlit(color);

                for (int p = 0; p < 60; p++)
                {
                    float angle = p * (360f / 60f) * Mathf.Deg2Rad;
                    lr.SetPosition(p, new Vector3(
                        Mathf.Cos(angle) * worldRadius,
                        0.15f,
                        Mathf.Sin(angle) * worldRadius
                    ));
                }

                zoneRings.Add(ringObj);
            }
        }

        private void CollectMiningResources()
        {
            if (PersistenceManager.Instance == null) return;

            foreach (var coord in activeMiningOutposts)
            {
                if (orePatches.TryGetValue(coord, out var patch))
                {
                    PersistenceManager.Instance.AddRunResource(patch.ResourceType, patch.BaseYield);

                    // Spawn fly-to-castle resource popup
                    Vector3 mineWorldPos = HexGrid.HexToWorld(coord) + Vector3.up * 2f;
                    Vector3 castleWorldPos = HexGrid.HexToWorld(new HexCoord(0, 0)) + Vector3.up * 2f;
                    var popup = new GameObject("ResourcePopup").AddComponent<UI.ResourcePopup>();
                    popup.Initialize(patch.BaseYield, patch.ResourceType, mineWorldPos, castleWorldPos);

                    Debug.Log($"Mined {patch.BaseYield} {patch.ResourceType} from outpost at {coord}");
                }
            }
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

                marker.AddComponent<UI.BillboardSprite>();

                // Create hex outline ring at ground level
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
                {
                    lr.SetPosition(i, new Vector3(corners[i].x, 0.15f, corners[i].z));
                }

                orePatchMarkers[kvp.Key] = marker;
            }
        }

        private void RemoveOrePatchMarker(HexCoord coord)
        {
            if (orePatchMarkers.TryGetValue(coord, out var marker))
            {
                if (marker != null) Destroy(marker);
                orePatchMarkers.Remove(coord);
            }
        }

        public void SetOreMarkersHighlighted(bool highlighted)
        {
            foreach (var kvp in orePatchMarkers)
            {
                if (kvp.Value == null) continue;

                float scale = highlighted ? 1.8f : 1f;
                kvp.Value.transform.localScale = Vector3.one * scale;

                var ring = kvp.Value.transform.Find("HexRing");
                if (ring != null)
                {
                    var lr = ring.GetComponent<LineRenderer>();
                    if (lr != null)
                    {
                        float width = highlighted ? 0.7f : 0.3f;
                        lr.startWidth = width;
                        lr.endWidth = width;

                        Color c = lr.material.color;
                        c.a = highlighted ? 0.7f : 0.25f;
                        lr.material.color = c;
                    }
                }
            }
        }

    }
}
