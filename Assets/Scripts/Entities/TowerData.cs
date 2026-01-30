using UnityEngine;

namespace TowerDefense.Entities
{
    [CreateAssetMenu(fileName = "TowerData", menuName = "Tower Defense/Tower Data")]
    public class TowerData : ScriptableObject
    {
        public string towerName;
        public int cost;
        public float damage;
        public float range;
        public float fireRate;
        public bool isAreaDamage;
        public float areaRadius;
        public bool appliesSlow;
        public float slowMultiplier;
        public float slowDuration;
        public Color towerColor = Color.blue;
    }
}