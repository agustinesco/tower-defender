using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Data;

namespace TowerDefense.Core
{
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager Instance { get; private set; }

        [SerializeField] private List<UpgradeCard> allUpgradeCards = new List<UpgradeCard>();

        // Active bonuses for current run
        private float towerSpeedBonus = 0f;
        private float towerDamageBonus = 0f;
        private float enemySpeedBonus = 0f;
        private float enemyGoldBonus = 0f;
        private int extraProjectiles = 0;

        private Dictionary<UpgradeCard, int> upgradeLevels = new Dictionary<UpgradeCard, int>();

        // Public accessors
        public float TowerSpeedBonus => towerSpeedBonus;
        public float TowerDamageBonus => towerDamageBonus;
        public float EnemySpeedBonus => enemySpeedBonus;
        public float EnemyGoldBonus => enemyGoldBonus;
        public int ExtraProjectiles => extraProjectiles;
        public IReadOnlyList<UpgradeCard> AllUpgradeCards => allUpgradeCards;

        public event System.Action OnUpgradesChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (LabManager.Instance != null)
            {
                towerDamageBonus = LabManager.Instance.BonusTowerDamage;
                towerSpeedBonus = LabManager.Instance.BonusTowerSpeed;
            }
        }

        public int GetLevel(UpgradeCard card)
        {
            return upgradeLevels.TryGetValue(card, out int level) ? level : 0;
        }

        public int GetNextCost(UpgradeCard card)
        {
            int level = GetLevel(card);
            return card.baseCost + level * card.costPerLevel;
        }

        public bool IsMaxLevel(UpgradeCard card)
        {
            return GetLevel(card) >= card.maxLevel;
        }

        public bool BuyUpgrade(UpgradeCard card)
        {
            if (IsMaxLevel(card)) return false;

            int cost = GetNextCost(card);
            if (PersistenceManager.Instance == null) return false;
            if (!PersistenceManager.Instance.SpendRunResource(card.costResource, cost)) return false;

            int newLevel = GetLevel(card) + 1;
            upgradeLevels[card] = newLevel;

            switch (card.effectType)
            {
                case UpgradeEffectType.TowerSpeed:
                    towerSpeedBonus += card.effectValue;
                    break;
                case UpgradeEffectType.TowerDamage:
                    towerDamageBonus += card.effectValue;
                    break;
                case UpgradeEffectType.EnemySpeedAndGold:
                    enemySpeedBonus += card.effectValue;
                    enemyGoldBonus += card.secondaryValue;
                    break;
                case UpgradeEffectType.ExtraProjectiles:
                    extraProjectiles += (int)card.effectValue;
                    break;
            }

            OnUpgradesChanged?.Invoke();
            Debug.Log($"Bought upgrade: {card.cardName} (Lv.{newLevel}). Speed bonus: {towerSpeedBonus}, Damage bonus: {towerDamageBonus}, Extra projectiles: {extraProjectiles}");
            return true;
        }

        public void ResetForNewRun()
        {
            var lab = LabManager.Instance;
            towerSpeedBonus = lab != null ? lab.BonusTowerSpeed : 0f;
            towerDamageBonus = lab != null ? lab.BonusTowerDamage : 0f;
            enemySpeedBonus = 0f;
            enemyGoldBonus = 0f;
            extraProjectiles = 0;
            upgradeLevels.Clear();
            OnUpgradesChanged?.Invoke();
        }

        public Dictionary<UpgradeCard, int> GetUpgradeLevels()
        {
            return new Dictionary<UpgradeCard, int>(upgradeLevels);
        }
    }
}
