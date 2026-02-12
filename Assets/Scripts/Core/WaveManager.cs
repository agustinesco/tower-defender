using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TowerDefense.Grid;
using TowerDefense.Entities;
using TowerDefense.Data;

namespace TowerDefense.Core
{
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private WaveData waveData;

        // Fallback values if no WaveData is assigned
        [Header("Fallback Settings (used if no WaveData assigned)")]
        [SerializeField] private float timeBetweenSpawns = 0.5f;
        [SerializeField] private int baseEnemiesPerWave = 5;

        private Dictionary<HexCoord, List<Vector3>> spawnPaths = new Dictionary<HexCoord, List<Vector3>>();
        private List<Enemy> activeEnemies = new List<Enemy>();
        private bool waveInProgress;
        private int currentWave;
        private bool isContinuousMode;
        private int continuousEnemyCount;

        // Cached WaitForSeconds to avoid GC
        private WaitForSeconds wait0_5s;
        private WaitForSeconds wait3_0s;

        // Reusable list for spawn path snapshots
        private readonly List<KeyValuePair<HexCoord, List<Vector3>>> pathsSnapshotReuse = new List<KeyValuePair<HexCoord, List<Vector3>>>();

        public bool WaveInProgress => waveInProgress;
        public int EnemyCount => activeEnemies.Count;
        public bool IsContinuousMode => isContinuousMode;

        public event System.Action OnWaveComplete;

        private void Start()
        {
            isContinuousMode = GameModeSelection.SelectedMode == Data.GameMode.Continuous;
            wait0_5s = new WaitForSeconds(0.5f);
            wait3_0s = new WaitForSeconds(3f);
            StartCoroutine(InitializePaths());
        }

        private IEnumerator InitializePaths()
        {
            // Wait for GameManager to generate map
            yield return new WaitForSeconds(0.2f);

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("WaveManager: GameManager.Instance is null!");
                yield break;
            }

            Debug.Log($"WaveManager: Found {gameManager.HexPieces.Count} hex pieces");
            Debug.Log($"WaveManager: Found {gameManager.SpawnPoints.Count} spawn points");

            var mapData = new Dictionary<HexCoord, HexPieceData>();
            foreach (var kvp in gameManager.HexPieces)
            {
                mapData[kvp.Key] = kvp.Value.Data;
            }

            var pathFinder = new PathFinder(mapData);

            foreach (var spawnPoint in gameManager.SpawnPoints)
            {
                Debug.Log($"WaveManager: Looking for path from spawn point {spawnPoint}");
                var path = pathFinder.FindPathToCastle(spawnPoint);
                if (path.Count > 0)
                {
                    PrependSpawnEdge(spawnPoint, path);
                    spawnPaths[spawnPoint] = path;
                    Debug.Log($"WaveManager: Path from {spawnPoint} has {path.Count} waypoints");
                }
                else
                {
                    Debug.LogWarning($"WaveManager: No path found from {spawnPoint} to castle!");
                }
            }

            Debug.Log($"WaveManager: Total paths initialized: {spawnPaths.Count}");
        }

        public void StartWave()
        {
            if (isContinuousMode) return;

            Debug.Log($"WaveManager: StartWave called. Wave in progress: {waveInProgress}, Paths count: {spawnPaths.Count}");

            if (waveInProgress)
            {
                Debug.Log("WaveManager: Wave already in progress, ignoring");
                return;
            }

            if (spawnPaths.Count == 0)
            {
                Debug.LogWarning("WaveManager: No spawn paths available! Cannot start wave.");
                return;
            }

            currentWave++;
            GameManager.Instance?.StartNextWave();
            StartCoroutine(SpawnWave());
        }

        private IEnumerator SpawnWave()
        {
            waveInProgress = true;
            AudioManager.Instance?.PlayWaveStart();

            var gm = GameManager.Instance;

            // Snapshot spawn paths, sorted by path length descending (furthest spawn points first)
            pathsSnapshotReuse.Clear();
            pathsSnapshotReuse.AddRange(spawnPaths);
            pathsSnapshotReuse.Sort((a, b) => b.Value.Count.CompareTo(a.Value.Count));
            var pathsSnapshot = pathsSnapshotReuse;

            if (waveData != null)
            {
                // Use configured wave data
                var wave = waveData.GetWave(currentWave);
                Debug.Log($"WaveManager: Spawning wave {currentWave} ({wave.waveName}) using WaveData");

                foreach (var kvp in pathsSnapshot)
                {
                    // Apply zone difficulty scaling
                    int spawnZone = gm != null ? gm.GetZone(kvp.Key) : 1;
                    float zoneHealthMul = gm != null ? gm.GetZoneHealthMultiplier(spawnZone) : 1f;
                    float zoneSpeedMul = gm != null ? gm.GetZoneSpeedMultiplier(spawnZone) : 1f;

                    Debug.Log($"WaveManager: Spawning at {kvp.Key} (zone {spawnZone}) with path of {kvp.Value.Count} waypoints");

                    foreach (var enemySpawn in wave.enemySpawns)
                    {
                        for (int i = 0; i < enemySpawn.count; i++)
                        {
                            SpawnEnemy(kvp.Value, enemySpawn.healthMultiplier * zoneHealthMul, enemySpawn.speedMultiplier * zoneSpeedMul);
                            yield return new WaitForSeconds(enemySpawn.spawnInterval);
                        }
                    }
                }
            }
            else
            {
                // Fallback: use old scaling system
                int enemiesPerSpawn = baseEnemiesPerWave + (currentWave - 1) * 2;
                Debug.Log($"WaveManager: Spawning wave {currentWave} with {enemiesPerSpawn} enemies per spawn point (fallback)");

                foreach (var kvp in pathsSnapshot)
                {
                    // Apply zone difficulty scaling
                    int spawnZone = gm != null ? gm.GetZone(kvp.Key) : 1;
                    float zoneHealthMul = gm != null ? gm.GetZoneHealthMultiplier(spawnZone) : 1f;
                    float zoneSpeedMul = gm != null ? gm.GetZoneSpeedMultiplier(spawnZone) : 1f;

                    Debug.Log($"WaveManager: Spawning at {kvp.Key} (zone {spawnZone}) with path of {kvp.Value.Count} waypoints");
                    for (int i = 0; i < enemiesPerSpawn; i++)
                    {
                        SpawnEnemy(kvp.Value, zoneHealthMul, zoneSpeedMul);
                        yield return new WaitForSeconds(timeBetweenSpawns);
                    }

                    // Spawn flying enemies starting from wave 3
                    if (currentWave >= 3)
                    {
                        int flyingCount = 1 + (currentWave - 3) / 2;
                        for (int i = 0; i < flyingCount; i++)
                        {
                            SpawnEnemy(kvp.Value, zoneHealthMul * 0.7f, zoneSpeedMul, Data.EnemyType.Flying);
                            yield return new WaitForSeconds(timeBetweenSpawns);
                        }

                        // Spawn cart enemies starting from wave 3
                        int cartCount = 1 + (currentWave - 3) / 3;
                        for (int i = 0; i < cartCount; i++)
                        {
                            SpawnEnemy(kvp.Value, zoneHealthMul, zoneSpeedMul, Data.EnemyType.Cart);
                            yield return new WaitForSeconds(timeBetweenSpawns * 2f);
                        }
                    }
                }
            }

            // Trigger goblin camp burst spawns
            var campSpawners = GameManager.Instance?.GoblinCampSpawners;
            if (campSpawners != null)
            {
                foreach (var spawner in campSpawners)
                {
                    var burstEnemies = spawner.SpawnBurst(currentWave);
                    foreach (var enemy in burstEnemies)
                    {
                        TrackEnemy(enemy);
                    }
                }
            }

            // Spawn lure enemies (from lure tiles, with bonus gold)
            SpawnLureEnemies();

            // Spawn boss enemies if entering new zones
            SpawnBosses();

            // Wait for all enemies to be defeated
            while (activeEnemies.Count > 0)
            {
                for (int i = activeEnemies.Count - 1; i >= 0; i--)
                {
                    if (activeEnemies[i] == null)
                        activeEnemies.RemoveAt(i);
                }
                yield return wait0_5s;
            }

            // Brief pause before ending wave
            yield return wait3_0s;

            waveInProgress = false;
            AudioManager.Instance?.PlayWaveComplete();
            int waveBonus = 50 + (currentWave - 1) * 10;
            GameManager.Instance?.AddCurrency(waveBonus);
            OnWaveComplete?.Invoke();
        }

        private Enemy SpawnEnemy(List<Vector3> path, float healthMultiplier, float speedMultiplier, Data.EnemyType type = Data.EnemyType.Ground)
        {
            var enemy = GameManager.Instance.SpawnEnemy(type, new List<Vector3>(path), currentWave, healthMultiplier, speedMultiplier);
            if (enemy != null)
                TrackEnemy(enemy);
            return enemy;
        }

        public void TrackEnemy(Enemy enemy)
        {
            enemy.OnDeath += HandleEnemyDeath;
            enemy.OnReachedCastle += HandleEnemyReachedCastle;
            activeEnemies.Add(enemy);
        }

        private void HandleEnemyDeath(Enemy enemy)
        {
            activeEnemies.Remove(enemy);
            QuestManager.Instance?.RecordKill();
        }

        private void HandleEnemyReachedCastle(Enemy enemy)
        {
            activeEnemies.Remove(enemy);
        }

        private void SpawnLureEnemies()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            var lureCoords = gm.ConsumeAndGetLures();
            if (lureCoords.Count == 0) return;

            var pathFinder = new PathFinder(gm.MapData);

            foreach (var coord in lureCoords)
            {
                var path = pathFinder.FindPathToCastle(coord);
                if (path.Count == 0)
                {
                    Debug.LogWarning($"WaveManager: No path from lure at {coord} to castle");
                    continue;
                }

                int count = 3 + currentWave;
                for (int i = 0; i < count; i++)
                {
                    var enemy = SpawnEnemy(path, 1f, 1f);
                    enemy.SetGoldMultiplier(gm.LureGoldMult);
                }

                Debug.Log($"WaveManager: Spawned {count} lure enemies at {coord} with {gm.LureGoldMult}x gold");
            }
        }

        private void SpawnBosses()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.HasPendingBoss) return;

            foreach (var bossZone in gm.PendingBossZones)
            {
                // Find the best spawn path for the boss (prefer paths in or near the boss zone)
                List<Vector3> bossPath = null;
                foreach (var kvp in spawnPaths)
                {
                    int spawnZone = gm.GetZone(kvp.Key);
                    if (spawnZone >= bossZone - 1)
                    {
                        bossPath = kvp.Value;
                        break;
                    }
                }

                // Fallback: use any spawn path
                if (bossPath == null)
                {
                    foreach (var kvp in spawnPaths)
                    {
                        bossPath = kvp.Value;
                        break;
                    }
                }

                if (bossPath == null) continue;

                float healthMul = gm.GetZoneHealthMultiplier(bossZone);
                float speedMul = gm.GetZoneSpeedMultiplier(bossZone);
                var boss = SpawnEnemy(bossPath, healthMul, speedMul);
                boss.MakeBoss();

                Debug.Log($"WaveManager: Boss spawned for zone {bossZone} (health x{healthMul}, speed x{speedMul})");
            }
        }

        public void RecalculatePaths()
        {
            Debug.Log("WaveManager: Recalculating paths after map expansion");

            spawnPaths.Clear();

            var gameManager = GameManager.Instance;
            if (gameManager == null) return;

            var mapData = new Dictionary<HexCoord, HexPieceData>();
            foreach (var kvp in gameManager.HexPieces)
            {
                mapData[kvp.Key] = kvp.Value.Data;
            }

            var pathFinder = new PathFinder(mapData);

            foreach (var spawnPoint in gameManager.SpawnPoints)
            {
                var path = pathFinder.FindPathToCastle(spawnPoint);
                if (path.Count > 0)
                {
                    PrependSpawnEdge(spawnPoint, path);
                    spawnPaths[spawnPoint] = path;
                    Debug.Log($"WaveManager: Recalculated path from {spawnPoint} with {path.Count} waypoints");
                }
                else
                {
                    Debug.LogWarning($"WaveManager: No path found from new spawn point {spawnPoint}");
                }
            }

            Debug.Log($"WaveManager: Paths recalculated. Total paths: {spawnPaths.Count}");
        }

        private void PrependSpawnEdge(HexCoord spawnCoord, List<Vector3> path)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.SpawnPointEdges.TryGetValue(spawnCoord, out int openEdge))
            {
                Vector3 edgeMidpoint = HexGrid.GetEdgeMidpoint(spawnCoord, openEdge);
                path.Insert(0, edgeMidpoint);
            }
        }

        public void StartContinuousMode()
        {
            isContinuousMode = true;
            waveInProgress = true;
            AudioManager.Instance?.PlayWaveStart();
            GameManager.Instance?.StartNextWave();
            StartCoroutine(ContinuousSpawnLoop());
        }

        private IEnumerator ContinuousSpawnLoop()
        {
            // Wait for paths to be ready
            while (spawnPaths.Count == 0)
            {
                yield return wait0_5s;
            }

            var gm = GameManager.Instance;
            float elapsed = 0f;
            var wait1s = new WaitForSeconds(1f);

            while (!gm.IsGameOver)
            {
                // Pick a random spawn path
                pathsSnapshotReuse.Clear();
                pathsSnapshotReuse.AddRange(spawnPaths);
                if (pathsSnapshotReuse.Count == 0)
                {
                    yield return wait1s;
                    continue;
                }

                var chosen = pathsSnapshotReuse[Random.Range(0, pathsSnapshotReuse.Count)];

                // Scale difficulty with time
                float difficultyScale = 1f + elapsed / 60f; // +1x per minute
                float healthMul = difficultyScale;
                float speedMul = 1f + elapsed / 300f; // slower speed ramp

                // Zone scaling
                int spawnZone = gm != null ? gm.GetZone(chosen.Key) : 1;
                float zoneHealthMul = gm != null ? gm.GetZoneHealthMultiplier(spawnZone) : 1f;
                float zoneSpeedMul = gm != null ? gm.GetZoneSpeedMultiplier(spawnZone) : 1f;

                // Determine enemy type
                Data.EnemyType type = Data.EnemyType.Ground;
                if (elapsed > 300f && Random.value < 0.15f)
                    type = Data.EnemyType.Cart;
                else if (elapsed > 30f && Random.value < 0.25f)
                    type = Data.EnemyType.Flying;

                SpawnEnemy(chosen.Value, healthMul * zoneHealthMul, speedMul * zoneSpeedMul, type);
                continuousEnemyCount++;

                // Interval decays from 2s to 0.3s over time
                float interval = Mathf.Max(0.3f, 2f - elapsed / 120f);
                yield return new WaitForSeconds(interval);

                elapsed += interval;
            }
        }
    }
}