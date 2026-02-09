using System.Collections.Generic;
using UnityEngine;
using TowerDefense.Grid;

namespace TowerDefense.Data
{
    [CreateAssetMenu(fileName = "NewPieceConfig", menuName = "Tower Defense/Hex Piece Config")]
    public class HexPieceConfig : ScriptableObject
    {
        [Header("Identity")]
        public HexPieceType pieceType;
        public string displayName;

        [Header("Placement")]
        public RotationPattern rotationPattern;

        [Header("Cost & Cooldown")]
        public int placementCost = 50;
        public float placementCooldown = 15f;

        [Header("Generation")]
        [Range(0f, 1f)] public float generationWeight;
        public bool startsInHand;

        [Header("Behavior")]
        public bool allowsTowerSlots = true;
        public bool isWaveSpawnPoint;

        [Header("Spawner")]
        public bool isSpawner;
        public float spawnerInterval = 8f;
        public float spawnerHealthMultiplier = 1f;
        public float spawnerSpeedMultiplier = 1f;

        [Header("UI")]
        public Color cardColor = new Color(0.3f, 0.3f, 0.3f);
        public List<float> previewAngles = new List<float>();
    }
}
