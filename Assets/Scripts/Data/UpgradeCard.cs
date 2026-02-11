using UnityEngine;

namespace TowerDefense.Data
{
    public enum UpgradeEffectType { TowerSpeed, TowerDamage, EnemySpeedAndGold, ExtraProjectiles, CriticalHit, GoldInterest, Restoration }

    [CreateAssetMenu(fileName = "NewUpgrade", menuName = "Tower Defense/Upgrade Card")]
    public class UpgradeCard : ScriptableObject
    {
        public string cardName;
        [TextArea(2, 4)]
        public string description;
        public UpgradeEffectType effectType;
        public float effectValue;
        public float secondaryValue;

        public ResourceType costResource;
        public int baseCost = 5;
        public int costPerLevel = 3;
        public int maxLevel = 10;
    }
}
