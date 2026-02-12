using UnityEngine;
using System.Collections.Generic;

namespace TowerDefense.Data
{
    [System.Serializable]
    public class EnemySpawnWeight
    {
        public EnemyType enemyType;
        [Range(0f, 1f)] public float weight = 1f;
    }

    [System.Serializable]
    public class DifficultyBlock
    {
        public string blockName;
        [Tooltip("Duration in seconds. 0 = infinite (last block)")]
        public float duration = 60f;
        public float spawnInterval = 1.5f;
        public float healthMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float goldMultiplier = 1f;
        public List<EnemySpawnWeight> enemies;
    }

    [CreateAssetMenu(fileName = "ContinuousDifficulty", menuName = "Tower Defense/Continuous Difficulty")]
    public class ContinuousDifficulty : ScriptableObject
    {
        public List<DifficultyBlock> blocks;
    }
}
