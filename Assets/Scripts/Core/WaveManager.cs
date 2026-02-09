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

        public bool WaveInProgress => waveInProgress;
        public int EnemyCount => activeEnemies.Count;

        public event System.Action OnWaveComplete;

        private void Start()
        {
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

            var gm = GameManager.Instance;

            // Snapshot spawn paths, sorted by path length descending (furthest spawn points first)
            var pathsSnapshot = new List<KeyValuePair<HexCoord, List<Vector3>>>(spawnPaths);
            pathsSnapshot.Sort((a, b) => b.Value.Count.CompareTo(a.Value.Count));

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

            // Route some enemies toward mining outposts
            TryRouteEnemiesToMines();

            // Spawn boss enemies if entering new zones
            SpawnBosses();

            // Wait for all enemies to be defeated
            while (activeEnemies.Count > 0)
            {
                activeEnemies.RemoveAll(e => e == null);
                yield return new WaitForSeconds(0.5f);
            }

            waveInProgress = false;
            int waveBonus = 50 + (currentWave - 1) * 10;
            GameManager.Instance?.AddCurrency(waveBonus);
            OnWaveComplete?.Invoke();
        }

        private Enemy SpawnEnemy(List<Vector3> path, float healthMultiplier, float speedMultiplier)
        {
            GameObject enemyObj = new GameObject("Enemy");
            var enemy = enemyObj.AddComponent<Enemy>();
            enemy.Initialize(new List<Vector3>(path), currentWave, healthMultiplier, speedMultiplier);

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

        private void TryRouteEnemiesToMines()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            var mines = gm.ActiveMiningOutposts;
            if (mines == null || mines.Count == 0) return;

            var pathFinder = new PathFinder(gm.MapData);

            foreach (var mineCoord in mines)
            {
                // Find a spawn path that can reach this mine
                foreach (var spawnKvp in spawnPaths)
                {
                    var pathToMine = pathFinder.FindPathToCoord(spawnKvp.Key, mineCoord);
                    if (pathToMine.Count == 0) continue;

                    int count = Mathf.Min(1 + currentWave / 3, 3);
                    for (int i = 0; i < count; i++)
                    {
                        var enemy = SpawnEnemy(pathToMine, 0.8f, 1.1f);
                        enemy.SetMineTarget(mineCoord);
                    }

                    Debug.Log($"WaveManager: Routed {count} enemies toward mine at {mineCoord}");
                    break; // One spawn point per mine is enough
                }
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
    }
}