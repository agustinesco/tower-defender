using UnityEngine;

namespace TowerDefense.Data
{
    [CreateAssetMenu(fileName = "WaveData", menuName = "Tower Defense/Wave Data")]
    public class WaveData : ScriptableObject
    {
        [System.Serializable]
        public class EnemySpawn
        {
            [Tooltip("Number of this enemy type to spawn")]
            public int count = 5;

            [Tooltip("Health multiplier for this enemy type")]
            public float healthMultiplier = 1f;

            [Tooltip("Speed multiplier for this enemy type")]
            public float speedMultiplier = 1f;

            [Tooltip("Delay between spawning each enemy of this type")]
            public float spawnInterval = 0.5f;
        }

        [System.Serializable]
        public class Wave
        {
            [Tooltip("Name for this wave (optional, for editor clarity)")]
            public string waveName;

            [Tooltip("Enemy spawns for this wave")]
            public EnemySpawn[] enemySpawns = new EnemySpawn[] { new EnemySpawn() };

            [Tooltip("Delay before next wave can start after this one completes")]
            public float delayAfterWave = 5f;
        }

        [Tooltip("List of waves. The last wave will repeat infinitely.")]
        public Wave[] waves = new Wave[] { new Wave() };

        public Wave GetWave(int waveNumber)
        {
            if (waves == null || waves.Length == 0)
            {
                return new Wave();
            }

            int index = waveNumber - 1;

            if (index < waves.Length)
            {
                return waves[index];
            }

            // Beyond defined waves: scale up from the last defined wave
            var lastWave = waves[waves.Length - 1];
            int extra = index - waves.Length + 1;

            var scaled = new Wave();
            scaled.waveName = $"Wave {waveNumber}";
            scaled.delayAfterWave = Mathf.Max(lastWave.delayAfterWave - extra * 0.2f, 2f);

            scaled.enemySpawns = new EnemySpawn[lastWave.enemySpawns.Length];
            for (int i = 0; i < lastWave.enemySpawns.Length; i++)
            {
                var b = lastWave.enemySpawns[i];
                scaled.enemySpawns[i] = new EnemySpawn
                {
                    count = b.count + extra * 3,
                    healthMultiplier = b.healthMultiplier * (1f + extra * 0.15f),
                    speedMultiplier = b.speedMultiplier * (1f + extra * 0.03f),
                    spawnInterval = Mathf.Max(b.spawnInterval - extra * 0.01f, 0.1f)
                };
            }

            return scaled;
        }
    }
}
