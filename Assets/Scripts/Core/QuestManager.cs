using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TowerDefense.Data;

namespace TowerDefense.Core
{
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        private List<QuestDefinition> allQuests;
        private string activeQuestId;
        private int killCount;
        private int tilesPlaced;
        private bool objectivesMet;

        public IReadOnlyList<QuestDefinition> AllQuests => allQuests;
        public string ActiveQuestId => activeQuestId;
        public bool HasActiveQuest => !string.IsNullOrEmpty(activeQuestId);
        public bool ObjectivesMet => objectivesMet;

        public event System.Action OnQuestAccepted;
        public event System.Action OnQuestProgressChanged;
        public event System.Action OnObjectivesMet;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            allQuests = Resources.LoadAll<QuestDefinition>("Quests")
                .OrderBy(q => q.questId)
                .ToList();
            activeQuestId = JsonSaveSystem.GetActiveQuestId();
        }

        public void ReloadFromSave()
        {
            activeQuestId = JsonSaveSystem.GetActiveQuestId();
            killCount = 0;
            tilesPlaced = 0;
            objectivesMet = false;
        }

        public QuestDefinition GetActiveQuest()
        {
            if (!HasActiveQuest) return null;
            for (int i = 0; i < allQuests.Count; i++)
            {
                if (allQuests[i].questId == activeQuestId)
                    return allQuests[i];
            }
            return null;
        }

        public void AcceptQuest(string questId)
        {
            activeQuestId = questId;
            JsonSaveSystem.SetActiveQuestId(questId);
            JsonSaveSystem.Save();
            OnQuestAccepted?.Invoke();
        }

        public void AbandonQuest()
        {
            activeQuestId = "";
            JsonSaveSystem.SetActiveQuestId("");
            JsonSaveSystem.Save();
        }

        public bool IsQuestCompleted(string questId)
        {
            return JsonSaveSystem.IsQuestCompleted(questId);
        }

        public bool IsQuestUnlocked(string questId)
        {
            for (int i = 0; i < allQuests.Count; i++)
            {
                if (allQuests[i].questId != questId) continue;
                var prereq = allQuests[i].prerequisiteQuestId;
                if (string.IsNullOrEmpty(prereq)) return true;
                return IsQuestCompleted(prereq);
            }
            return false;
        }

        public void StartRun()
        {
            killCount = 0;
            tilesPlaced = 0;
            objectivesMet = false;
        }

        public void RecordKill()
        {
            killCount++;
            CheckProgress();
        }

        public void RecordTilePlaced()
        {
            tilesPlaced++;
            CheckProgress();
        }

        public void OnResourcesChanged()
        {
            CheckProgress();
        }

        public int GetObjectiveProgress(QuestObjective objective)
        {
            switch (objective.objectiveType)
            {
                case QuestObjectiveType.GatherResource:
                    if (PersistenceManager.Instance != null)
                        return PersistenceManager.Instance.GetRunGathered(objective.resourceType);
                    return 0;
                case QuestObjectiveType.KillEnemies:
                    return killCount;
                case QuestObjectiveType.ExpandTiles:
                    return tilesPlaced;
                default:
                    return 0;
            }
        }

        public bool IsObjectiveMet(QuestObjective objective)
        {
            return GetObjectiveProgress(objective) >= objective.requiredAmount;
        }

        public void CompleteActiveQuest()
        {
            var quest = GetActiveQuest();
            if (quest == null) return;
            if (!AreAllObjectivesMet(quest)) return;

            JsonSaveSystem.MarkQuestCompleted(quest.questId);
            JsonSaveSystem.Save();

            if (quest.rewardAmount > 0 && PersistenceManager.Instance != null)
                PersistenceManager.Instance.AddBankedResource(quest.rewardResource, quest.rewardAmount);

            if (!string.IsNullOrEmpty(quest.unlockLabUpgrade) && LabManager.Instance != null)
                LabManager.Instance.ForceUnlock(quest.unlockLabUpgrade);

            Debug.Log($"Quest '{quest.questName}' completed! Reward: {quest.rewardAmount} {quest.rewardResource}");
        }

        private void CheckProgress()
        {
            if (objectivesMet) return;

            var quest = GetActiveQuest();
            if (quest == null) return;

            OnQuestProgressChanged?.Invoke();

            if (AreAllObjectivesMet(quest))
            {
                objectivesMet = true;
                OnObjectivesMet?.Invoke();
            }
        }

        private bool AreAllObjectivesMet(QuestDefinition quest)
        {
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                if (!IsObjectiveMet(quest.objectives[i]))
                    return false;
            }
            return quest.objectives.Count > 0;
        }
    }
}