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
        public Sprite towerIcon;
        public float projectileSpeed = 15f;

        [Header("Shotgun Settings")]
        public bool isShotgun = false;
        public int shotgunProjectileCount = 5;
        public float shotgunSpreadAngle = 60f;

        [Header("Tesla Settings")]
        public bool isTesla = false;
        public int bounceCount = 3;
        public float bounceRange = 10f;
        public float damageFalloff = 0.7f;

        [Header("Flame Settings")]
        public bool isFlame = false;
        public float firePatchDuration = 3f;
        public float fireDamagePerSecond = 3f;
        public float burnDuration = 2f;

        [Header("Mortar Settings")]
        public bool isMortar = false;
        public float minRange = 0f;

        [Header("Targeting")]
        public bool canTargetFlying = false;
        public bool prioritizeFlying = false;

        [Header("Placement")]
        public float placementRadius = 10f;
    }
}