using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Entities;
using TowerDefense.Data;

namespace TowerDefense.Core
{
    public class EnemyManager : MonoBehaviour
    {
        public static EnemyManager Instance { get; private set; }

        private readonly List<Enemy> activeEnemies = new List<Enemy>();

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

        public Enemy GetClosestEnemy(Vector3 position, float maxRange)
        {
            return GetClosestEnemyFiltered(position, maxRange, canTargetFlying: true, prioritizeFlying: false);
        }

        public Enemy GetClosestEnemyFiltered(Vector3 position, float maxRange, bool canTargetFlying, bool prioritizeFlying)
        {
            Enemy closest = null;
            float closestDist = maxRange;
            Enemy closestFlying = null;
            float closestFlyingDist = maxRange;

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var enemy = activeEnemies[i];
                if (enemy == null || enemy.IsDead) continue;
                if (enemy.IsFlying && !canTargetFlying) continue;

                float dist = Vector3.Distance(position, enemy.transform.position);
                if (dist > maxRange) continue;

                if (dist < closestDist)
                {
                    closest = enemy;
                    closestDist = dist;
                }

                if (prioritizeFlying && enemy.IsFlying && dist < closestFlyingDist)
                {
                    closestFlying = enemy;
                    closestFlyingDist = dist;
                }
            }

            // If prioritizing flying and one was found, prefer it
            if (prioritizeFlying && closestFlying != null)
                return closestFlying;

            return closest;
        }

        public bool HasEnemyInRange(Vector3 position, float range, bool canTargetFlying = true)
        {
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var enemy = activeEnemies[i];
                if (enemy == null || enemy.IsDead) continue;
                if (enemy.IsFlying && !canTargetFlying) continue;

                float dist = Vector3.Distance(position, enemy.transform.position);
                if (dist <= range)
                    return true;
            }
            return false;
        }

        public Enemy GetClosestEnemyExcluding(Vector3 position, float maxRange, HashSet<Enemy> exclude, bool canTargetFlying = true)
        {
            Enemy closest = null;
            float closestDist = maxRange;

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var enemy = activeEnemies[i];
                if (enemy == null || enemy.IsDead || exclude.Contains(enemy)) continue;
                if (enemy.IsFlying && !canTargetFlying) continue;

                float dist = Vector3.Distance(position, enemy.transform.position);
                if (dist <= closestDist)
                {
                    closest = enemy;
                    closestDist = dist;
                }
            }

            return closest;
        }

        public void GetEnemiesInRange(Vector3 position, float range, List<Enemy> results, bool canTargetFlying = true)
        {
            results.Clear();
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var enemy = activeEnemies[i];
                if (enemy == null || enemy.IsDead) continue;
                if (enemy.IsFlying && !canTargetFlying) continue;

                float dist = Vector3.Distance(position, enemy.transform.position);
                if (dist <= range)
                    results.Add(enemy);
            }
        }
    }
}
