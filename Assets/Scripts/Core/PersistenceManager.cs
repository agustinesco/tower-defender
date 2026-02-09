using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Data;

namespace TowerDefense.Core
{
    public class PersistenceManager : MonoBehaviour
    {
        public static PersistenceManager Instance { get; private set; }

        private Dictionary<ResourceType, int> bankedResources = new Dictionary<ResourceType, int>();
        private Dictionary<ResourceType, int> runResources = new Dictionary<ResourceType, int>();

        public event System.Action OnResourcesChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        public int GetBanked(ResourceType type)
        {
            return bankedResources.TryGetValue(type, out int val) ? val : 0;
        }

        public int GetRunGathered(ResourceType type)
        {
            return runResources.TryGetValue(type, out int val) ? val : 0;
        }

        public void AddRunResource(ResourceType type, int amount)
        {
            if (!runResources.ContainsKey(type))
                runResources[type] = 0;
            runResources[type] += amount;
            OnResourcesChanged?.Invoke();
        }

        public void BankRunResources()
        {
            foreach (var kvp in runResources)
            {
                if (!bankedResources.ContainsKey(kvp.Key))
                    bankedResources[kvp.Key] = 0;
                bankedResources[kvp.Key] += kvp.Value;
            }
            runResources.Clear();
            Save();
            OnResourcesChanged?.Invoke();
        }

        public void BankPartialResources(float fraction)
        {
            foreach (var kvp in runResources)
            {
                int amount = Mathf.RoundToInt(kvp.Value * fraction);
                if (amount <= 0) continue;
                if (!bankedResources.ContainsKey(kvp.Key))
                    bankedResources[kvp.Key] = 0;
                bankedResources[kvp.Key] += amount;
            }
            runResources.Clear();
            Save();
            OnResourcesChanged?.Invoke();
        }

        public bool SpendBanked(ResourceType type, int amount)
        {
            if (GetBanked(type) < amount) return false;
            bankedResources[type] -= amount;
            Save();
            OnResourcesChanged?.Invoke();
            return true;
        }

        public bool SpendRunResource(ResourceType type, int amount)
        {
            if (GetRunGathered(type) < amount) return false;
            runResources[type] -= amount;
            OnResourcesChanged?.Invoke();
            return true;
        }

        public void ResetRun()
        {
            runResources.Clear();
            OnResourcesChanged?.Invoke();
        }

        private void Save()
        {
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                string key = $"resource_{type}";
                PlayerPrefs.SetInt(key, GetBanked(type));
            }
            PlayerPrefs.Save();
        }

        private void Load()
        {
            bankedResources.Clear();
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                string key = $"resource_{type}";
                bankedResources[type] = PlayerPrefs.GetInt(key, 0);
            }
        }
    }
}
