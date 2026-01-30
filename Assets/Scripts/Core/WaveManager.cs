using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TowerDefense.Grid;
using TowerDefense.Entities;

namespace TowerDefense.Core
{
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private float timeBetweenWaves = 20f;
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
            int enemiesPerSpawn = baseEnemiesPerWave + (currentWave - 1) * 2;

            Debug.Log($"WaveManager: Spawning wave {currentWave} with {enemiesPerSpawn} enemies per spawn point");

            foreach (var kvp in spawnPaths)
            {
                Debug.Log($"WaveManager: Spawning at {kvp.Key} with path of {kvp.Value.Count} waypoints");
                for (int i = 0; i < enemiesPerSpawn; i++)
                {
                    SpawnEnemy(kvp.Value);
                    yield return new WaitForSeconds(timeBetweenSpawns);
                }
            }

            // Wait for all enemies to be defeated
            while (activeEnemies.Count > 0)
            {
                activeEnemies.RemoveAll(e => e == null);
                yield return new WaitForSeconds(0.5f);
            }

            waveInProgress = false;
            GameManager.Instance?.AddCurrency(50); // Wave bonus
            OnWaveComplete?.Invoke();
        }

        private void SpawnEnemy(List<Vector3> path)
        {
            GameObject enemyObj = new GameObject("Enemy");
            var enemy = enemyObj.AddComponent<Enemy>();
            enemy.Initialize(new List<Vector3>(path), currentWave);

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