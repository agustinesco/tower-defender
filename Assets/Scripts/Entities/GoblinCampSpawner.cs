using System.Collections.Generic;
using UnityEngine;
using TowerDefense.Core;
using TowerDefense.Grid;

namespace TowerDefense.Entities
{
    public class GoblinCampSpawner : MonoBehaviour
    {
        private List<Vector3> pathToCastle;
        private HexCoord coord;
        private float healthMultiplier = 0.7f;
        private float speedMultiplier = 0.85f;

        public HexCoord Coord => coord;

        public void Initialize(HexCoord coord, List<Vector3> path, float healthMul = 0.7f, float speedMul = 0.85f)
        {
            this.coord = coord;
            pathToCastle = path;
            healthMultiplier = healthMul;
            speedMultiplier = speedMul;
        }

        public void UpdatePath(List<Vector3> newPath)
        {
            pathToCastle = newPath;
        }

        public List<Enemy> SpawnBurst(int waveNumber)
        {
            var enemies = new List<Enemy>();
            if (pathToCastle == null || pathToCastle.Count == 0) return enemies;

            var gm = GameManager.Instance;
            GameObject prefab = gm != null ? gm.GroundEnemyPrefab : null;

            int count = 2 + waveNumber;
            for (int i = 0; i < count; i++)
            {
                GameObject enemyObj;
                Enemy enemy;

                if (prefab != null)
                {
                    enemyObj = Object.Instantiate(prefab);
                    enemyObj.name = "GoblinCampEnemy";
                    enemy = enemyObj.GetComponent<Enemy>();
                    if (enemy == null)
                        enemy = enemyObj.AddComponent<Enemy>();
                }
                else
                {
                    enemyObj = new GameObject("GoblinCampEnemy");
                    enemy = enemyObj.AddComponent<Enemy>();
                }

                enemy.Initialize(new List<Vector3>(pathToCastle), waveNumber, healthMultiplier, speedMultiplier);
                enemies.Add(enemy);
            }
            return enemies;
        }
    }
}
