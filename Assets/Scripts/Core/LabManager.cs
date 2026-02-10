using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Data;

namespace TowerDefense.Core
{
    public class LabManager : MonoBehaviour
    {
        public static LabManager Instance { get; private set; }

        [SerializeField] private List<LabUpgrade> upgrades = new List<LabUpgrade>();

        private Dictionary<string, int> purchasedLevels = new Dictionary<string, int>();

        public IReadOnlyList<LabUpgrade> Upgrades => upgrades;

        public event System.Action OnLabChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeDefaultUpgrades();
            Load();
        }

        private void InitializeDefaultUpgrades()
        {
            if (upgrades != null && upgrades.Count > 0) return;

            upgrades = new List<LabUpgrade>
            {
                new LabUpgrade
                {
                    upgradeName = "Gold Reserve",
                    description = "+50 starting gold",
                    upgradeType = LabUpgradeType.StartingGold,
                    valuePerLevel = 50f,
                    maxLevel = 5,
                    costResource = ResourceType.IronOre,
                    baseCost = 5,
                    costPerLevel = 5
                },
                new LabUpgrade
                {
                    upgradeName = "Fortification",
                    description = "+1 starting life",
                    upgradeType = LabUpgradeType.ExtraLives,
                    valuePerLevel = 1f,
                    maxLevel = 3,
                    costResource = ResourceType.Gems,
                    baseCost = 10,
                    costPerLevel = 10
                },
                new LabUpgrade
                {
                    upgradeName = "Heavy Caliber",
                    description = "+10% tower damage",
                    upgradeType = LabUpgradeType.TowerDamage,
                    valuePerLevel = 0.1f,
                    maxLevel = 5,
                    costResource = ResourceType.Florpus,
                    baseCost = 8,
                    costPerLevel = 6
                },
                new LabUpgrade
                {
                    upgradeName = "Rapid Fire",
                    description = "+10% tower speed",
                    upgradeType = LabUpgradeType.TowerSpeed,
                    valuePerLevel = 0.1f,
                    maxLevel = 5,
                    costResource = ResourceType.IronOre,
                    baseCost = 8,
                    costPerLevel = 6
                },
                new LabUpgrade
                {
                    upgradeName = "Quick Deploy",
                    description = "-10% piece cooldown",
                    upgradeType = LabUpgradeType.PieceCooldownReduction,
                    valuePerLevel = 0.1f,
                    maxLevel = 3,
                    costResource = ResourceType.Adamantite,
                    baseCost = 12,
                    costPerLevel = 8
                },
                new LabUpgrade
                {
                    upgradeName = "Vital Force",
                    description = "+14 max HP",
                    upgradeType = LabUpgradeType.MaxHP,
                    valuePerLevel = 14f,
                    maxLevel = 5,
                    costResource = ResourceType.Gems,
                    baseCost = 8,
                    costPerLevel = 6
                },
                new LabUpgrade
                {
                    upgradeName = "Slow",
                    description = "Unlock the Slow tower",
                    upgradeType = LabUpgradeType.TowerUnlock,
                    valuePerLevel = 1f,
                    maxLevel = 1,
                    costResource = ResourceType.IronOre,
                    baseCost = 10,
                    costPerLevel = 0
                },
                new LabUpgrade
                {
                    upgradeName = "Flame",
                    description = "Unlock the Flame tower",
                    upgradeType = LabUpgradeType.TowerUnlock,
                    valuePerLevel = 1f,
                    maxLevel = 1,
                    costResource = ResourceType.Florpus,
                    baseCost = 15,
                    costPerLevel = 0
                },
                new LabUpgrade
                {
                    upgradeName = "Shotgun",
                    description = "Unlock the Shotgun tower",
                    upgradeType = LabUpgradeType.TowerUnlock,
                    valuePerLevel = 1f,
                    maxLevel = 1,
                    costResource = ResourceType.Gems,
                    baseCost = 20,
                    costPerLevel = 0
                },
                new LabUpgrade
                {
                    upgradeName = "Tesla",
                    description = "Unlock the Tesla tower",
                    upgradeType = LabUpgradeType.TowerUnlock,
                    valuePerLevel = 1f,
                    maxLevel = 1,
                    costResource = ResourceType.Adamantite,
                    baseCost = 25,
                    costPerLevel = 0
                }
            };
        }

        public int GetLevel(LabUpgrade upgrade)
        {
            return purchasedLevels.TryGetValue(upgrade.upgradeName, out int lvl) ? lvl : 0;
        }

        public bool CanPurchase(LabUpgrade upgrade)
        {
            int level = GetLevel(upgrade);
            if (level >= upgrade.maxLevel) return false;

            int cost = upgrade.GetCost(level);
            if (PersistenceManager.Instance == null) return false;
            return PersistenceManager.Instance.GetBanked(upgrade.costResource) >= cost;
        }

        public bool Purchase(LabUpgrade upgrade)
        {
            if (!CanPurchase(upgrade)) return false;

            int level = GetLevel(upgrade);
            int cost = upgrade.GetCost(level);

            if (!PersistenceManager.Instance.SpendBanked(upgrade.costResource, cost))
                return false;

            purchasedLevels[upgrade.upgradeName] = level + 1;
            Save();
            OnLabChanged?.Invoke();

            Debug.Log($"Lab: Purchased {upgrade.upgradeName} level {level + 1} for {cost} {upgrade.costResource}");
            return true;
        }

        public void UnlockAllTowers()
        {
            foreach (var upgrade in upgrades)
            {
                if (upgrade.upgradeType == LabUpgradeType.TowerUnlock)
                    purchasedLevels[upgrade.upgradeName] = upgrade.maxLevel;
            }
            Save();
        }

        public bool IsTowerUnlocked(string towerName)
        {
            foreach (var upgrade in upgrades)
            {
                if (upgrade.upgradeType == LabUpgradeType.TowerUnlock &&
                    upgrade.upgradeName == towerName)
                {
                    return GetLevel(upgrade) >= 1;
                }
            }
            // No unlock entry means the tower is available by default
            return true;
        }

        // --- Bonus accessors ---

        public int BonusStartingGold => Mathf.RoundToInt(GetTotalBonus(LabUpgradeType.StartingGold));
        public int BonusStartingLives => Mathf.RoundToInt(GetTotalBonus(LabUpgradeType.ExtraLives));
        public float BonusTowerDamage => GetTotalBonus(LabUpgradeType.TowerDamage);
        public float BonusTowerSpeed => GetTotalBonus(LabUpgradeType.TowerSpeed);
        public float PieceCooldownReduction => GetTotalBonus(LabUpgradeType.PieceCooldownReduction);
        public int BonusMaxHP => Mathf.RoundToInt(GetTotalBonus(LabUpgradeType.MaxHP));

        private float GetTotalBonus(LabUpgradeType type)
        {
            float total = 0f;
            foreach (var upgrade in upgrades)
            {
                if (upgrade.upgradeType == type)
                    total += GetLevel(upgrade) * upgrade.valuePerLevel;
            }
            return total;
        }

        // --- Save / Load ---

        private void Save()
        {
            foreach (var kvp in purchasedLevels)
            {
                PlayerPrefs.SetInt($"lab_{kvp.Key}", kvp.Value);
            }
            PlayerPrefs.Save();
        }

        private void Load()
        {
            purchasedLevels.Clear();
            foreach (var upgrade in upgrades)
            {
                string key = $"lab_{upgrade.upgradeName}";
                if (PlayerPrefs.HasKey(key))
                {
                    purchasedLevels[upgrade.upgradeName] = PlayerPrefs.GetInt(key);
                }
            }
        }
    }
}
