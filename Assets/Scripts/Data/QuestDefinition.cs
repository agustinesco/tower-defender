using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Data
{
    [System.Serializable]
    public class QuestObjective
    {
        public QuestObjectiveType objectiveType;
        public ResourceType resourceType; // Only used for GatherResource
        public int requiredAmount;
    }

    [CreateAssetMenu(fileName = "NewQuest", menuName = "Tower Defense/Quest Definition")]
    public class QuestDefinition : ScriptableObject
    {
        public string questId;
        public string questName;
        [TextArea] public string description;
        public List<QuestObjective> objectives;
        public ResourceType rewardResource;
        public int rewardAmount;
        public string prerequisiteQuestId;  // Quest that must be completed to unlock this one (empty = always unlocked)
        public string unlockLabUpgrade;     // Lab upgrade name to force-unlock on completion (empty = none)
        public MapConfig mapConfig;         // null = use GameManager defaults
    }
}