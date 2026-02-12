using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using TowerDefense.Data;

namespace TowerDefense.Core
{
    [Serializable]
    public class SaveData
    {
        // Serialized as parallel arrays (JsonUtility can't serialize Dictionary)
        public List<string> resourceKeys = new List<string>();
        public List<int> resourceValues = new List<int>();

        public List<string> labKeys = new List<string>();
        public List<int> labValues = new List<int>();

        public List<string> tutorialStepsSeen = new List<string>();
        public bool labTutorialComplete;
        public bool questEscapeTutComplete;

        // Quest data
        public string activeQuestId = "";
        public List<string> completedQuestIds = new List<string>();

        // Runtime dictionaries (not serialized, rebuilt from lists)
        [NonSerialized] public Dictionary<string, int> resources;
        [NonSerialized] public Dictionary<string, int> labLevels;

        public void BuildDictionaries()
        {
            resources = new Dictionary<string, int>();
            for (int i = 0; i < resourceKeys.Count && i < resourceValues.Count; i++)
                resources[resourceKeys[i]] = resourceValues[i];

            labLevels = new Dictionary<string, int>();
            for (int i = 0; i < labKeys.Count && i < labValues.Count; i++)
                labLevels[labKeys[i]] = labValues[i];
        }

        public void SyncFromDictionaries()
        {
            resourceKeys.Clear();
            resourceValues.Clear();
            if (resources != null)
            {
                foreach (var kvp in resources)
                {
                    resourceKeys.Add(kvp.Key);
                    resourceValues.Add(kvp.Value);
                }
            }

            labKeys.Clear();
            labValues.Clear();
            if (labLevels != null)
            {
                foreach (var kvp in labLevels)
                {
                    labKeys.Add(kvp.Key);
                    labValues.Add(kvp.Value);
                }
            }
        }
    }

    public static class JsonSaveSystem
    {
        private const string FileName = "save.json";
        private static SaveData cachedData;
        private static bool dirty;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static SaveData Data
        {
            get
            {
                if (cachedData == null)
                    Load();
                return cachedData;
            }
        }

        public static void Load()
        {
            string path = FilePath;
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    cachedData = JsonUtility.FromJson<SaveData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load save file, creating new: {e.Message}");
                    cachedData = null;
                }
            }

            if (cachedData == null)
                cachedData = new SaveData();

            if (cachedData.resourceKeys == null) cachedData.resourceKeys = new List<string>();
            if (cachedData.resourceValues == null) cachedData.resourceValues = new List<int>();
            if (cachedData.labKeys == null) cachedData.labKeys = new List<string>();
            if (cachedData.labValues == null) cachedData.labValues = new List<int>();
            if (cachedData.tutorialStepsSeen == null) cachedData.tutorialStepsSeen = new List<string>();
            if (cachedData.activeQuestId == null) cachedData.activeQuestId = "";
            if (cachedData.completedQuestIds == null) cachedData.completedQuestIds = new List<string>();

            cachedData.BuildDictionaries();
            dirty = false;
        }

        public static void Save()
        {
            if (cachedData == null) return;
            try
            {
                cachedData.SyncFromDictionaries();
                string json = JsonUtility.ToJson(cachedData, true);
                File.WriteAllText(FilePath, json);
                dirty = false;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save: {e.Message}");
            }
        }

        public static void MarkDirty()
        {
            dirty = true;
        }

        public static void SaveIfDirty()
        {
            if (dirty)
                Save();
        }

        public static void DeleteAll()
        {
            cachedData = new SaveData();
            cachedData.BuildDictionaries();
            Save();
        }

        // --- Resource helpers ---

        public static int GetBankedResource(ResourceType type)
        {
            string key = type.ToString();
            return Data.resources.TryGetValue(key, out int val) ? val : 0;
        }

        public static void SetBankedResource(ResourceType type, int value)
        {
            Data.resources[type.ToString()] = value;
            dirty = true;
        }

        // --- Lab helpers ---

        public static int GetLabLevel(string upgradeName)
        {
            return Data.labLevels.TryGetValue(upgradeName, out int val) ? val : 0;
        }

        public static void SetLabLevel(string upgradeName, int level)
        {
            Data.labLevels[upgradeName] = level;
            dirty = true;
        }

        // --- Tutorial helpers ---

        public static bool IsTutorialStepSeen(string stepName)
        {
            return Data.tutorialStepsSeen.Contains(stepName);
        }

        public static void MarkTutorialStepSeen(string stepName)
        {
            if (!Data.tutorialStepsSeen.Contains(stepName))
            {
                Data.tutorialStepsSeen.Add(stepName);
                dirty = true;
            }
        }

        public static void ClearTutorialSteps()
        {
            Data.tutorialStepsSeen.Clear();
            Data.labTutorialComplete = false;
            Data.questEscapeTutComplete = false;
            dirty = true;
        }

        // --- Quest helpers ---

        public static string GetActiveQuestId()
        {
            return Data.activeQuestId ?? "";
        }

        public static void SetActiveQuestId(string questId)
        {
            Data.activeQuestId = questId ?? "";
            dirty = true;
        }

        public static bool IsQuestCompleted(string questId)
        {
            return Data.completedQuestIds.Contains(questId);
        }

        public static void MarkQuestCompleted(string questId)
        {
            if (!Data.completedQuestIds.Contains(questId))
            {
                Data.completedQuestIds.Add(questId);
                dirty = true;
            }
        }

        // --- Migration from PlayerPrefs ---

        public static void MigrateFromPlayerPrefs()
        {
            bool migrated = false;

            // Migrate banked resources
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                string key = $"resource_{type}";
                if (PlayerPrefs.HasKey(key))
                {
                    Data.resources[type.ToString()] = PlayerPrefs.GetInt(key, 0);
                    PlayerPrefs.DeleteKey(key);
                    migrated = true;
                }
            }

            // Migrate lab levels
            string[] knownLabUpgrades = {
                "Gold Reserve", "Fortification", "Heavy Caliber", "Rapid Fire",
                "Quick Deploy", "Vital Force", "Slow", "Flame", "Shotgun", "Tesla",
                "Lure", "Haste", "GoldenTouch", "Cross", "Star", "Crossroads"
            };
            foreach (string name in knownLabUpgrades)
            {
                string key = $"lab_{name}";
                if (PlayerPrefs.HasKey(key))
                {
                    Data.labLevels[name] = PlayerPrefs.GetInt(key, 0);
                    PlayerPrefs.DeleteKey(key);
                    migrated = true;
                }
            }

            // Migrate tutorial steps
            string[] tutSteps = {
                "SelectPathCard", "BuildOnOre", "MineExplanation",
                "SpawnExplanation", "PathEditInfo", "SwitchToTowers",
                "SelectTower", "PlaceTower", "StartWave", "Complete"
            };
            foreach (string step in tutSteps)
            {
                string key = $"tut_{step}";
                if (PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key, 0) == 1)
                {
                    if (!Data.tutorialStepsSeen.Contains(step))
                        Data.tutorialStepsSeen.Add(step);
                    PlayerPrefs.DeleteKey(key);
                    migrated = true;
                }
            }

            // Migrate lab tutorial
            string labTutKey = "tut_lab_upgrade";
            if (PlayerPrefs.HasKey(labTutKey))
            {
                Data.labTutorialComplete = PlayerPrefs.GetInt(labTutKey, 0) == 1;
                PlayerPrefs.DeleteKey(labTutKey);
                migrated = true;
            }

            // Migrate quest data
            if (PlayerPrefs.HasKey("quest_active"))
            {
                Data.activeQuestId = PlayerPrefs.GetString("quest_active", "");
                PlayerPrefs.DeleteKey("quest_active");
                migrated = true;
            }

            // Migrate completed quests (check known quest IDs)
            for (int i = 1; i <= 20; i++)
            {
                string qKey = $"quest_done_q{i:D3}";
                if (PlayerPrefs.HasKey(qKey))
                {
                    string qid = $"q{i:D3}";
                    if (!Data.completedQuestIds.Contains(qid))
                        Data.completedQuestIds.Add(qid);
                    PlayerPrefs.DeleteKey(qKey);
                    migrated = true;
                }
            }

            // Migrate quest escape tutorial
            if (PlayerPrefs.HasKey("tut_quest_escape"))
            {
                Data.questEscapeTutComplete = PlayerPrefs.GetInt("tut_quest_escape", 0) == 1;
                PlayerPrefs.DeleteKey("tut_quest_escape");
                migrated = true;
            }

            if (migrated)
            {
                Save();
                PlayerPrefs.Save();
                Debug.Log("Migrated save data from PlayerPrefs to JSON.");
            }
        }
    }
}
