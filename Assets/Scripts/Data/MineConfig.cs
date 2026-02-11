using UnityEngine;

namespace TowerDefense.Data
{
    [CreateAssetMenu(fileName = "MineConfig", menuName = "Tower Defense/Mine Config")]
    public class MineConfig : ScriptableObject
    {
        [Header("Resource Generation")]
        [Tooltip("Multiplier applied to the base yield of each ore patch")]
        public int yieldMultiplier = 1;

        [Header("Continuous Mode")]
        [Tooltip("Seconds between resource collections in continuous mode")]
        public float collectionInterval = 30f;
    }
}
