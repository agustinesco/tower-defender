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
        [SerializeField] private bool useFreeTowerPlacement = false;
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
        [SerializeField] private Sprite minecartSprite;
        [SerializeField] private Sprite mineEntranceSprite;

        [Header("Prefabs")]
        [SerializeField] private GameObject hexPiecePrefab;

        [Header("Mine Config")]
        [SerializeField] private MineConfig mineConfig;

        [Header("Enemy Configs")]
        [SerializeField] private List<EnemyData> enemyConfigs;

        [Header("Piece Configs")]
        [SerializeField] private List<HexPieceConfig> pieceConfigs;

        private Dictionary<HexPieceType, HexPieceConfig> pieceConfigLookup;
        private Dictionary<EnemyType, EnemyData> enemyDataLookup;
        private Dictionary<HexCoord, HexPiece> hexPieces = new Dictionary<HexCoord, HexPiece>();
        private Dictionary<HexCoord, HexPieceData> mapData = new Dictionary<HexCoord, HexPieceData>();
        private List<HexCoord> spawnPoints = new List<HexCoord>();
        private Dictionary<HexCoord, int> spawnPointEdges = new Dictionary<HexCoord, int>();
        private MapGenerator mapGenerator;
        private WaveManager waveManager;
        public WaveManager WaveManagerRef => waveManager;
        private UpgradeSelectionUI upgradeSelectionUI;
        private TowerManager towerManager;

        private PieceProvider pieceProvider;
        private PlacementValidator placementValidator;
        private GhostPieceManager ghostPieceManager;
        private PieceDragHandler pieceDragHandler;
        private PieceHandUI pieceHandUI;
        private TerrainManager terrainManager;

        private List<GoblinCampSpawner> goblinCampSpawners = new List<GoblinCampSpawner>();
        private HashSet<HexCoord> hiddenSpawners = new HashSet<HexCoord>();

        private Dictionary<HexCoord, OrePatch> orePatches = new Dictionary<HexCoord, OrePatch>();
        private Dictionary<HexCoord, GameObject> orePatchMarkers = new Dictionary<HexCoord, GameObject>();
        private HashSet<HexCoord> activeMiningOutposts = new HashSet<HexCoord>();
        private Dictionary<HexCoord, GameObject> mineVisuals = new Dictionary<HexCoord, GameObject>();
        private Dictionary<HexCoord, UI.MineTimerIndicator> mineTimerIndicators = new Dictionary<HexCoord, UI.MineTimerIndicator>();

        private int newPathsPlaced = 0;
        [Header("Path Price Escalation")]
        [SerializeField] private float pathPriceScale = 1f;
        [SerializeField] private float pathPriceExponent = 1.5f;

        // Runtime fields resolved from MapConfig or serialized defaults
        private int[] activeZoneBoundaries;
        private float activePathPriceScale;
        private float activePathPriceExponent;
        private float activeBuildGracePeriod;
        private float activeZoneHealthStep;
        private float activeZoneSpeedStep;

        private const int MineCost = 100;
        private const int LureCost = 75;
        private const float LureGoldMultiplier = 2f;
        private const int HasteCost = 400;
        private const int GoldenTouchCost = 500;
        private const float ModContinuousDuration = 120f;

        // Zone system — fallback when no MapConfig is active
        [Header("Zone System (fallback)")]
        [SerializeField] private ZoneConfig[] zones = {
            new ZoneConfig { width = 3, resourceTypes = new[] { ResourceType.IronOre, ResourceType.Gems }, oreNodeCount = 6 },
            new ZoneConfig { width = 3, resourceTypes = new[] { ResourceType.Florpus, ResourceType.Adamantite }, oreNodeCount = 4 },
            new ZoneConfig { width = 3 }
        };
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
        private bool objectivesMet = false;

        private float ContinuousMineInterval => mineConfig != null ? mineConfig.collectionInterval : 30f;
        private int MineYieldMultiplier => mineConfig != null ? mineConfig.yieldMultiplier : 1;
        private Dictionary<HexCoord, float> mineTimers = new Dictionary<HexCoord, float>();
        private readonly List<HexCoord> _readyMines = new List<HexCoord>();
        private readonly List<HexCoord> _mineKeySnapshot = new List<HexCoord>();

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

        public EnemyData GetEnemyData(EnemyType type) => enemyDataLookup != null && enemyDataLookup.TryGetValue(type, out var d) ? d : null;

        public Enemy SpawnEnemy(EnemyType type, List<Vector3> path, int waveNumber, float healthMul = 1f, float speedMul = 1f)
        {
            var data = GetEnemyData(type);
            if (data == null || data.prefab == null)
            {
                Debug.LogError($"No EnemyData or prefab for type {type}");
                return null;
            }
            var obj = Instantiate(data.prefab);
            obj.name = data.enemyName;
            var enemy = obj.GetComponent<Enemy>();
            if (enemy == null)
                enemy = obj.AddComponent<Enemy>();
            enemy.Initialize(data, path, waveNumber, healthMul, speedMul);
            return enemy;
        }

        public event System.Action<int> OnLivesChanged;
        public event System.Action<int> OnCurrencyChanged;
        public event System.Action<int> OnWaveChanged;
        public event System.Action OnGameOver;
        public event System.Action OnBuildPhaseStarted;
        public event System.Action OnBuildPhaseEnded;
        public event System.Action OnObjectivesMet;

        public bool ObjectivesMet => objectivesMet;

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
            if (QuestManager.Instance == null)
            {
                var questObj = new GameObject("QuestManager");
                questObj.AddComponent<QuestManager>();
            }

            // Initialize currency/lives in Awake so other scripts' Start() reads correct values
            var earlyConfig = ResolveMapConfig();
            int baseLives = earlyConfig != null ? earlyConfig.startingLives : startingLives;
            int baseCurrency = earlyConfig != null ? earlyConfig.startingCurrency : startingCurrency;
            currentLives = baseLives + (LabManager.Instance != null ? LabManager.Instance.BonusStartingLives + LabManager.Instance.BonusMaxHP : 0);
            maxLives = currentLives;
            currentCurrency = baseCurrency + (LabManager.Instance != null ? LabManager.Instance.BonusStartingGold : 0);
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

        private MapConfig ResolveMapConfig()
        {
            return QuestManager.Instance?.GetActiveQuest()?.mapConfig;
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

            // Build enemy data lookup
            enemyDataLookup = new Dictionary<EnemyType, EnemyData>();
            if (enemyConfigs != null)
            {
                foreach (var config in enemyConfigs)
                    enemyDataLookup[config.enemyType] = config;
            }

            // 0. Create EnemyManager (per-scene, not DontDestroyOnLoad)
            if (EnemyManager.Instance == null)
            {
                var enemyMgrObj = new GameObject("EnemyManager");
                enemyMgrObj.AddComponent<EnemyManager>();
            }

            // 0b. Create AudioManager
            if (AudioManager.Instance == null)
            {
                var audioObj = new GameObject("AudioManager");
                audioObj.AddComponent<AudioManager>();
            }

            // Resolve MapConfig from active quest (null = use serialized defaults)
            var mapConfig = ResolveMapConfig();
            var activeZones = mapConfig != null ? mapConfig.zones : zones;
            activeZoneBoundaries = mapConfig != null ? mapConfig.GetZoneBoundaries() : ComputeZoneBoundaries(zones);
            activePathPriceScale = mapConfig != null ? mapConfig.pathPriceScale : pathPriceScale;
            activePathPriceExponent = mapConfig != null ? mapConfig.pathPriceExponent : pathPriceExponent;
            activeBuildGracePeriod = mapConfig != null ? mapConfig.buildGracePeriod : buildGracePeriod;
            activeZoneHealthStep = mapConfig != null ? mapConfig.zoneHealthStep : 0.5f;
            activeZoneSpeedStep = mapConfig != null ? mapConfig.zoneSpeedStep : 0.1f;

            // 1. Generate castle and starting path
            mapGenerator = new MapGenerator();
            mapData = mapGenerator.GenerateInitialCastle();

            // 1a. Auto-generate starting tiles
            int tileCount = mapConfig != null ? mapConfig.startingTiles : startingTiles;
            var startingPieces = mapGenerator.GenerateStartingPath(tileCount);
            foreach (var piece in startingPieces)
            {
                mapData[piece.Coord] = piece;
            }

            // Create all initial pieces visually
            foreach (var kvp in mapData)
            {
                CreateHexPiece(kvp.Value);
            }

            // 1b. Generate hidden spawner positions
            int spawnerCount = mapConfig != null ? mapConfig.hiddenSpawnerCount : 0;
            if (spawnerCount > 0)
            {
                int spMinDist = mapConfig != null ? mapConfig.hiddenSpawnerMinDistance : 3;
                int spMaxDist = mapConfig != null ? mapConfig.hiddenSpawnerMaxDistance : 7;
                hiddenSpawners = mapGenerator.GenerateHiddenSpawners(spawnerCount, spMinDist, spMaxDist);
            }
            else
            {
                hiddenSpawners = new HashSet<HexCoord>();
            }

            // 1c. Generate ore patches and create visual markers
            if (activeZones != null && activeZones.Length > 0)
            {
                int oreMin = mapConfig != null ? mapConfig.oreMinDistance : 2;
                int oreMax = mapConfig != null ? mapConfig.oreMaxDistance : 6;
                bool guarantee = mapConfig != null ? mapConfig.guaranteeStartingOre : true;
                ResourceType guaranteeType = mapConfig != null ? mapConfig.guaranteedOreType : ResourceType.IronOre;
                orePatches = mapGenerator.GenerateOrePatches(
                    oreMin, oreMax, activeZoneBoundaries, activeZones,
                    guarantee, guaranteeType);
            }
            else
            {
                int zone1Boundary = activeZoneBoundaries.Length > 0 ? activeZoneBoundaries[0] : 3;
                orePatches = mapGenerator.GenerateOrePatches(minDistance: 2, maxDistance: 6, zoneBoundary: zone1Boundary);
            }
            CreateOrePatchMarkers();

            // 1e. Create zone boundary visuals
            CreateZoneRings();

            // 1f. Initialize terrain
            var terrainObj = new GameObject("TerrainManager");
            terrainManager = terrainObj.AddComponent<TerrainManager>();
            terrainManager.Initialize();
            if (activeZoneBoundaries != null && activeZoneBoundaries.Length > 0)
                terrainManager.SetMaxPlacementDistance(activeZoneBoundaries[activeZoneBoundaries.Length - 1]);
            terrainManager.UpdateTerrain(mapData);

            // 2. Initialize piece provider (filter out locked pieces)
            var availablePieces = new List<HexPieceConfig>();
            if (pieceConfigs != null)
            {
                foreach (var config in pieceConfigs)
                {
                    if (LabManager.Instance == null || LabManager.Instance.IsPieceUnlocked(config.pieceType.ToString()))
                        availablePieces.Add(config);
                }
            }
            pieceProvider = new PieceProvider(availablePieces);
            pieceProvider.Initialize();
            pieceProvider.OnHandChanged += OnHandChanged;

            // 3. Initialize placement validator
            placementValidator = new PlacementValidator(mapData, pieceConfigLookup);
            if (activeZoneBoundaries != null && activeZoneBoundaries.Length > 0)
                placementValidator.SetMaxPlacementDistance(activeZoneBoundaries[activeZoneBoundaries.Length - 1]);
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
                if (mapConfig != null)
                    waveManager.SetMapConfig(mapConfig);
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
                if (towerManager != null)
                {
                    pieceHandUI.SetFreeTowerMode(true);
                    pieceHandUI.SetAvailableTowers(towerManager.AllTowers);
                }

                pieceDragHandler.Initialize(placementValidator, ghostPieceManager, pieceHandUI);
                pieceHandUI.RefreshHand(pieceProvider.Pieces);
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.StartRun();
                QuestManager.Instance.OnObjectivesMet += OnQuestObjectivesMet;
            }
            if (PersistenceManager.Instance != null && QuestManager.Instance != null)
                PersistenceManager.Instance.OnResourcesChanged += QuestManager.Instance.OnResourcesChanged;

            Invoke(nameof(FireInitialEvents), 0.1f);
        }

        private void OnQuestObjectivesMet()
        {
            if (objectivesMet) return;
            objectivesMet = true;
            OnObjectivesMet?.Invoke();
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

            if (IsContinuousMode && mineTimers.Count > 0 && waveManager != null && waveManager.WaveInProgress)
            {
                float dt = Time.deltaTime;
                float interval = ContinuousMineInterval;
                _readyMines.Clear();
                _mineKeySnapshot.Clear();
                _mineKeySnapshot.AddRange(mineTimers.Keys);
                for (int i = 0; i < _mineKeySnapshot.Count; i++)
                {
                    var coord = _mineKeySnapshot[i];
                    float t = mineTimers[coord] + dt;
                    if (t >= interval)
                        _readyMines.Add(coord);
                    else
                        mineTimers[coord] = t;
                }
                for (int i = 0; i < _readyMines.Count; i++)
                {
                    var coord = _readyMines[i];
                    mineTimers[coord] = 0f;
                    CollectFromMine(coord);
                    if (mineTimerIndicators.TryGetValue(coord, out var indicator) && indicator != null)
                        indicator.SetTimer(0f);
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

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnObjectivesMet -= OnQuestObjectivesMet;
                if (PersistenceManager.Instance != null)
                    PersistenceManager.Instance.OnResourcesChanged -= QuestManager.Instance.OnResourcesChanged;
            }
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

            bool isNewPlacement = !hexPieces.ContainsKey(coord);
            int cost = isNewPlacement ? GetPieceCost(config.placementCost) : config.placementCost;

            // Check and spend gold
            if (!SpendCurrency(cost))
                return;

            // Track new path placements for price escalation
            if (isNewPlacement)
                newPathsPlaced++;

            // Handle replacement of existing piece
            if (!isNewPlacement && hexPieces.TryGetValue(coord, out var existingPiece))
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

            // Update terrain ring
            if (terrainManager != null)
                terrainManager.UpdateTerrain(mapData);

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

            QuestManager.Instance?.RecordTilePlaced();

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
                var visual = CreateMineVisual(hex.transform, hex.Data);
                mineVisuals[coord] = visual;
            }

            RemoveOrePatchMarker(coord);

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

        public int GetPieceCost(int baseCost)
        {
            if (newPathsPlaced <= 0) return baseCost;
            return baseCost + Mathf.RoundToInt(activePathPriceScale * Mathf.Pow(newPathsPlaced, activePathPriceExponent));
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

        public void AddLife(int amount)
        {
            currentLives = Mathf.Min(currentLives + amount, maxLives);
            OnLivesChanged?.Invoke(currentLives);
        }

        public void LoseLife()
        {
            currentLives--;
            OnLivesChanged?.Invoke(currentLives);

            AudioManager.Instance?.PlayCastleHit();

            // Screen shake
            var cam = FindFirstObjectByType<CameraController>();
            cam?.Shake(0.3f, 1.5f);

            // Red flash overlay
            var hud = FindFirstObjectByType<HUDController>();
            hud?.FlashDamage();

            // Castle damage popup at castle position
            Vector3 castlePos = HexGrid.HexToWorld(new HexCoord(0, 0)) + Vector3.up * 3f;
            UI.CastleDamagePopup.GetFromPool(castlePos);

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
            // Complete quest before banking resources
            QuestManager.Instance?.CompleteActiveQuest();
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

            if (IsContinuousMode)
                StartMineTimers();
        }

        private void StartMineTimers()
        {
            foreach (var coord in activeMiningOutposts)
            {
                if (mineTimers.ContainsKey(coord)) continue;
                mineTimers[coord] = 0f;
                Vector3 worldPos = HexGrid.HexToWorld(coord);
                var indicatorObj = new GameObject("MineTimer");
                indicatorObj.transform.position = worldPos + Vector3.up * 10f;
                var indicator = indicatorObj.AddComponent<UI.MineTimerIndicator>();
                indicator.Initialize(ContinuousMineInterval);
                mineTimerIndicators[coord] = indicator;
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
            buildTimer = activeBuildGracePeriod;
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
            MainSceneController.LoadMainMenu();
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
                var visual = CreateMineVisual(hex.transform, hex.Data);
                mineVisuals[coord] = visual;
            }

            // Remove the ore patch marker
            RemoveOrePatchMarker(coord);

            var patch = orePatches[coord];
            Debug.Log($"Mine built at {coord}: {patch.ResourceType} (yield: {patch.BaseYield}/wave)");
            return true;
        }

        private GameObject CreateMineVisual(Transform parent, HexPieceData pieceData)
        {
            var container = new GameObject("MineDecor");
            container.transform.SetParent(parent);
            container.transform.localPosition = Vector3.zero;
            container.transform.localScale = Vector3.one;

            // Tint the hex base to a light grey to indicate a mine tile
            var hexBase = parent.Find("HexBase");
            if (hexBase != null)
            {
                var rend = hexBase.GetComponent<MeshRenderer>();
                if (rend != null)
                    rend.material = MaterialCache.CreateUnlit(new Color(0.85f, 0.85f, 0.88f));
            }

            // Pick the first connected edge to place sprites along
            if (pieceData.ConnectedEdges == null || pieceData.ConnectedEdges.Count == 0)
                return container;

            int edge = pieceData.ConnectedEdges[0];
            Vector3 dir = HexMeshGenerator.GetEdgeDirection(edge);
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x);

            // Place sprites in the outer area of the piece, near the hex boundary
            Vector3 midpoint = dir * HexGrid.InnerRadius * 0.35f;
            float sideOffset = 12f;
            float spriteY = 1.0f;

            // MineEntrance on one side
            if (mineEntranceSprite != null)
            {
                var entranceObj = new GameObject("MineEntrance");
                entranceObj.transform.SetParent(container.transform);
                entranceObj.transform.localPosition = midpoint + perp * sideOffset + Vector3.up * spriteY;
                var sr = entranceObj.AddComponent<SpriteRenderer>();
                sr.sprite = mineEntranceSprite;
                entranceObj.AddComponent<UI.BillboardSprite>();
            }

            // Minecart on the other side
            if (minecartSprite != null)
            {
                var cartObj = new GameObject("Minecart");
                cartObj.transform.SetParent(container.transform);
                cartObj.transform.localPosition = midpoint - perp * sideOffset + Vector3.up * spriteY;
                var sr = cartObj.AddComponent<SpriteRenderer>();
                sr.sprite = minecartSprite;
                cartObj.AddComponent<UI.BillboardSprite>();
            }

            return container;
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

        private static int[] ComputeZoneBoundaries(ZoneConfig[] zoneConfigs)
        {
            if (zoneConfigs == null || zoneConfigs.Length == 0)
                return new int[] { 3, 6, 9 };

            var boundaries = new int[zoneConfigs.Length];
            int cumulative = 0;
            for (int i = 0; i < zoneConfigs.Length; i++)
            {
                cumulative += zoneConfigs[i].width;
                boundaries[i] = cumulative;
            }
            return boundaries;
        }

        public int GetZone(HexCoord coord)
        {
            int dist = new HexCoord(0, 0).DistanceTo(coord);
            var bounds = activeZoneBoundaries ?? ComputeZoneBoundaries(zones);
            for (int i = 0; i < bounds.Length; i++)
            {
                if (dist <= bounds[i])
                    return i + 1;
            }
            return bounds.Length + 1;
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
            return 1f + (zone - 1) * activeZoneHealthStep;
        }

        public float GetZoneSpeedMultiplier(int zone)
        {
            return 1f + (zone - 1) * activeZoneSpeedStep;
        }

        private void CreateZoneRings()
        {
            var bounds = activeZoneBoundaries ?? ComputeZoneBoundaries(zones);
            Color[] ringColors = new Color[]
            {
                new Color(0.8f, 0.8f, 0.2f, 0.6f),  // yellow
                new Color(0.8f, 0.5f, 0.2f, 0.6f),  // orange
                new Color(0.8f, 0.2f, 0.2f, 0.6f),  // red
                new Color(0.6f, 0.1f, 0.1f, 0.6f),  // dark red
            };

            for (int i = 0; i < bounds.Length; i++)
            {
                bool isOuterBoundary = i == bounds.Length - 1;
                Color color = isOuterBoundary
                    ? new Color(0.8f, 0.15f, 0.15f, 1f)
                    : ringColors[Mathf.Min(i, ringColors.Length - 1)];
                float width = isOuterBoundary ? 3f : 1.5f;
                string name = isOuterBoundary ? "BoundaryRing" : $"ZoneRing_{i + 2}";

                CreateHexZoneRing(bounds[i], color, width, name);
            }
        }

        private void CreateHexZoneRing(int distance, Color color, float width, string name)
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

            // Build adjacency: map each vertex position to its segment indices
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

            // Chain segments into an ordered closed path
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

            var ringObj = new GameObject(name);
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

        private void CollectMiningResources()
        {
            if (PersistenceManager.Instance == null) return;

            foreach (var coord in activeMiningOutposts)
                CollectFromMine(coord);
        }

        private void CollectFromMine(HexCoord coord)
        {
            if (PersistenceManager.Instance == null) return;
            if (!orePatches.TryGetValue(coord, out var patch)) return;

            int yield = patch.BaseYield * MineYieldMultiplier;
            PersistenceManager.Instance.AddRunResource(patch.ResourceType, yield);

            Vector3 mineWorldPos = HexGrid.HexToWorld(coord) + Vector3.up * 2f;
            Vector3 castleWorldPos = HexGrid.HexToWorld(new HexCoord(0, 0)) + Vector3.up * 2f;
            UI.ResourcePopup.GetFromPool(yield, patch.ResourceType, mineWorldPos, castleWorldPos);

            Debug.Log($"Mined {yield} {patch.ResourceType} from outpost at {coord}");
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
