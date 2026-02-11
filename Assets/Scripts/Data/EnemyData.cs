using UnityEngine;

namespace TowerDefense.Data
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Tower Defense/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        public string enemyName;
        public EnemyType enemyType;
        public float baseSpeed = 2f;
        public float baseHealth = 10f;
        public int baseCurrencyReward = 10;
        public float flyHeight = 0f;
        public GameObject prefab;
    }
}
