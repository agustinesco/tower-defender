using UnityEngine;

namespace TowerDefense.Data
{
    [System.Serializable]
    public class ZoneConfig
    {
        public int width = 3;
        public ResourceType[] resourceTypes;
        public int oreNodeCount = 4;
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
        public ZoneConfig[] zones;
        public float zoneHealthStep = 0.5f;
        public float zoneSpeedStep = 0.1f;

        [Header("Ore Generation")]
        public int oreMinDistance = 2;
        public int oreMaxDistance = 6;
        public bool guaranteeStartingOre = true;
        public ResourceType guaranteedOreType = ResourceType.IronOre;

        [Header("Wave Data")]
        public WaveData waveData;
        public ContinuousDifficulty continuousDifficulty;
        public int waveBaseBonus = 50;
        public int waveBonusPerWave = 10;

        [Header("Path Price Escalation")]
        public float pathPriceScale = 1f;
        public float pathPriceExponent = 1.5f;

        [Header("Extraction")]
        public float extractionCountdown = 30f;

        [Header("Hidden Spawners")]
        public int hiddenSpawnerCount = 0;
        public int hiddenSpawnerMinDistance = 3;
        public int hiddenSpawnerMaxDistance = 7;

        /// <summary>
        /// Computes cumulative zone boundaries from zone widths.
        /// E.g. zones with widths [5, 4, 3] => boundaries [5, 9, 12].
        /// </summary>
        public int[] GetZoneBoundaries()
        {
            if (zones == null || zones.Length == 0)
                return new int[] { 3, 6, 9 };

            var boundaries = new int[zones.Length];
            int cumulative = 0;
            for (int i = 0; i < zones.Length; i++)
            {
                cumulative += zones[i].width;
                boundaries[i] = cumulative;
            }
            return boundaries;
        }
    }
}
