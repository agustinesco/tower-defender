namespace TowerDefense.Data
{
    public enum LabUpgradeType
    {
        StartingGold,
        ExtraLives,
        TowerDamage,
        TowerSpeed,
        PieceCooldownReduction,
        TowerUnlock,
        MaxHP,
        ModUnlock
    }

    [System.Serializable]
    public class LabUpgrade
    {
        public string upgradeName;
        public string description;
        public LabUpgradeType upgradeType;
        public float valuePerLevel;
        public int maxLevel;
        public ResourceType costResource;
        public int baseCost;
        public int costPerLevel;

        public int GetCost(int currentLevel)
        {
            return baseCost + currentLevel * costPerLevel;
        }
    }
}
