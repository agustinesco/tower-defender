using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Entities;
using TowerDefense.Data;

namespace TowerDefense.Core
{
    public class EnemyManager : MonoBehaviour
    {
        public static EnemyManager Instance { get; private set; }

        private const float CellSize = 20f;
        private const float InvCellSize = 1f / CellSize;

        private readonly List<Enemy> activeEnemies = new List<Enemy>();
        private readonly Dictionary<long, List<Enemy>> grid = new Dictionary<long, List<Enemy>>();
        private readonly Queue<List<Enemy>> listPool = new Queue<List<Enemy>>();
        private readonly List<long> reusableKeys = new List<long>();

        public IReadOnlyList<Enemy> ActiveEnemies => activeEnemies;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Register(Enemy enemy)
        {
            activeEnemies.Add(enemy);
        }

        public void Unregister(Enemy enemy)
        {
            activeEnemies.Remove(enemy);
        }

        private static long CellKey(int cx, int cz)
        {
            return ((long)cx << 32) | (uint)cz;
        }

        private static void WorldToCell(Vector3 pos, out int cx, out int cz)
        {
            cx = Mathf.FloorToInt(pos.x * InvCellSize);
            cz = Mathf.FloorToInt(pos.z * InvCellSize);
        }

        private void LateUpdate()
        {
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            // Return all lists to pool and clear grid
            foreach (var kvp in grid)
            {
                kvp.Value.Clear();
                listPool.Enqueue(kvp.Value);
            }
            grid.Clear();

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var enemy = activeEnemies[i];
                if (enemy == null || enemy.IsDead) continue;

                WorldToCell(enemy.transform.position, out int cx, out int cz);
                long key = CellKey(cx, cz);

                if (!grid.TryGetValue(key, out var bucket))
                {
                    bucket = listPool.Count > 0 ? listPool.Dequeue() : new List<Enemy>();
                    grid[key] = bucket;
                }
                bucket.Add(enemy);
            }
        }

        // Iterate only the grid cells that overlap with the query range
        private void GetCellRange(Vector3 position, float range, out int minCx, out int minCz, out int maxCx, out int maxCz)
        {
            minCx = Mathf.FloorToInt((position.x - range) * InvCellSize);
            minCz = Mathf.FloorToInt((position.z - range) * InvCellSize);
            maxCx = Mathf.FloorToInt((position.x + range) * InvCellSize);
            maxCz = Mathf.FloorToInt((position.z + range) * InvCellSize);
        }

        public Enemy GetClosestEnemy(Vector3 position, float maxRange)
        {
            return GetClosestEnemyFiltered(position, maxRange, canTargetFlying: true, prioritizeFlying: false);
        }

        public Enemy GetClosestEnemyFiltered(Vector3 position, float maxRange, bool canTargetFlying, bool prioritizeFlying)
        {
            Enemy closest = null;
            float closestDistSq = maxRange * maxRange;
            Enemy closestFlying = null;
            float closestFlyingDistSq = maxRange * maxRange;

            GetCellRange(position, maxRange, out int minCx, out int minCz, out int maxCx, out int maxCz);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                {
                    long key = CellKey(cx, cz);
                    if (!grid.TryGetValue(key, out var bucket)) continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        var enemy = bucket[i];
                        if (enemy.IsFlying && !canTargetFlying) continue;

                        float dx = position.x - enemy.transform.position.x;
                        float dz = position.z - enemy.transform.position.z;
                        float distSq = dx * dx + dz * dz;

                        if (distSq > closestDistSq && distSq > closestFlyingDistSq) continue;

                        if (distSq < closestDistSq)
                        {
                            closest = enemy;
                            closestDistSq = distSq;
                        }

                        if (prioritizeFlying && enemy.IsFlying && distSq < closestFlyingDistSq)
                        {
                            closestFlying = enemy;
                            closestFlyingDistSq = distSq;
                        }
                    }
                }
            }

            if (prioritizeFlying && closestFlying != null)
                return closestFlying;

            return closest;
        }

        public bool HasEnemyInRange(Vector3 position, float range, bool canTargetFlying = true)
        {
            float rangeSq = range * range;
            GetCellRange(position, range, out int minCx, out int minCz, out int maxCx, out int maxCz);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                {
                    long key = CellKey(cx, cz);
                    if (!grid.TryGetValue(key, out var bucket)) continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        var enemy = bucket[i];
                        if (enemy.IsFlying && !canTargetFlying) continue;

                        float dx = position.x - enemy.transform.position.x;
                        float dz = position.z - enemy.transform.position.z;
                        if (dx * dx + dz * dz <= rangeSq)
                            return true;
                    }
                }
            }
            return false;
        }

        public Enemy GetClosestEnemyExcluding(Vector3 position, float maxRange, HashSet<Enemy> exclude, bool canTargetFlying = true)
        {
            Enemy closest = null;
            float closestDistSq = maxRange * maxRange;

            GetCellRange(position, maxRange, out int minCx, out int minCz, out int maxCx, out int maxCz);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                {
                    long key = CellKey(cx, cz);
                    if (!grid.TryGetValue(key, out var bucket)) continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        var enemy = bucket[i];
                        if (exclude.Contains(enemy)) continue;
                        if (enemy.IsFlying && !canTargetFlying) continue;

                        float dx = position.x - enemy.transform.position.x;
                        float dz = position.z - enemy.transform.position.z;
                        float distSq = dx * dx + dz * dz;

                        if (distSq < closestDistSq)
                        {
                            closest = enemy;
                            closestDistSq = distSq;
                        }
                    }
                }
            }

            return closest;
        }

        public void GetEnemiesInRange(Vector3 position, float range, List<Enemy> results, bool canTargetFlying = true)
        {
            results.Clear();
            float rangeSq = range * range;

            GetCellRange(position, range, out int minCx, out int minCz, out int maxCx, out int maxCz);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                {
                    long key = CellKey(cx, cz);
                    if (!grid.TryGetValue(key, out var bucket)) continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        var enemy = bucket[i];
                        if (enemy.IsFlying && !canTargetFlying) continue;

                        float dx = position.x - enemy.transform.position.x;
                        float dz = position.z - enemy.transform.position.z;
                        if (dx * dx + dz * dz <= rangeSq)
                            results.Add(enemy);
                    }
                }
            }
        }
        public Enemy GetClosestEnemyInBand(Vector3 position, float minRange, float maxRange, bool canTargetFlying = true)
        {
            Enemy closest = null;
            float closestDistSq = maxRange * maxRange;
            float minRangeSq = minRange * minRange;

            GetCellRange(position, maxRange, out int minCx, out int minCz, out int maxCx, out int maxCz);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                {
                    long key = CellKey(cx, cz);
                    if (!grid.TryGetValue(key, out var bucket)) continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        var enemy = bucket[i];
                        if (enemy.IsFlying && !canTargetFlying) continue;

                        float dx = position.x - enemy.transform.position.x;
                        float dz = position.z - enemy.transform.position.z;
                        float distSq = dx * dx + dz * dz;

                        if (distSq >= minRangeSq && distSq < closestDistSq)
                        {
                            closest = enemy;
                            closestDistSq = distSq;
                        }
                    }
                }
            }

            return closest;
        }
    }
}
