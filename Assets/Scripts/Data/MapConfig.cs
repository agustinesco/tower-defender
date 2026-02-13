using UnityEngine;

namespace TowerDefense.Data
{
    [System.Serializable]
    public class ZoneOreConfig
    {
        public ResourceType[] resourceTypes;
        public int nodeCount = 6;
    }

    [CreateAssetMenu(fileName = "MapConfig", menuName = "Tower Defense/Map Config")]
    public class MapConfig : ScriptableObject
    {
        [Header("Starting Resources")]
        public int startingLives = 30;
        public int startingCurrency = 200;

        [Header("Starting Layout")]
        public int startingTiles = 2;

        [Header("Build Phase")]
        public float buildGracePeriod = 30f;

        [Header("Zone System")]
        public int[] zoneBoundaries = { 3, 6, 9 };
        public float zoneHealthStep = 0.5f;
        public float zoneSpeedStep = 0.1f;

        [Header("Ore Generation")]
        public int oreMinDistance = 2;
        public int oreMaxDistance = 6;
        public bool guaranteeStartingOre = true;
        public ResourceType guaranteedOreType = ResourceType.IronOre;
        public ZoneOreConfig[] zoneOreConfigs;

        [Header("Wave Data")]
        public WaveData waveData;
        public ContinuousDifficulty continuousDifficulty;
        public int waveBaseBonus = 50;
        public int waveBonusPerWave = 10;

        [Header("Path Price Escalation")]
        public float pathPriceScale = 1f;
        public float pathPriceExponent = 1.5f;

        [Header("Hidden Spawners")]
        public int hiddenSpawnerCount = 0;
        public int hiddenSpawnerMinDistance = 3;
        public int hiddenSpawnerMaxDistance = 7;
    }
}
