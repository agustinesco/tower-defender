using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Grid;
using TowerDefense.UI;

namespace TowerDefense.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int startingLives = 10;
        [SerializeField] private int startingCurrency = 200;

        [Header("Map Expansion")]
        [SerializeField] private float baseForkChance = 0.1f;
        [SerializeField] private float forkChanceIncreasePerWave = 0.05f;
        [SerializeField] private float maxForkChance = 0.5f;

        [Header("Materials")]
        [SerializeField] private Material hexBaseMaterial;
        [SerializeField] private Material hexPathMaterial;
        [SerializeField] private Material castleMaterial;

        [Header("Prefabs")]
        [SerializeField] private GameObject hexPiecePrefab;

        private Dictionary<HexCoord, HexPiece> hexPieces = new Dictionary<HexCoord, HexPiece>();
        private Dictionary<HexCoord, HexPieceData> mapData = new Dictionary<HexCoord, HexPieceData>();
        private List<HexCoord> spawnPoints = new List<HexCoord>();
        private MapGenerator mapGenerator;
        private WaveManager waveManager;

        private int currentLives;
        private int currentCurrency;
        private int currentWave = 0;
        private bool gameOver = false;
        private float currentForkChance;

        public int Lives => currentLives;
        public int Currency => currentCurrency;
        public int Wave => currentWave;
        public bool IsGameOver => gameOver;
        public Dictionary<HexCoord, HexPiece> HexPieces => hexPieces;
        public List<HexCoord> SpawnPoints => spawnPoints;

        public event System.Action<int> OnLivesChanged;
        public event System.Action<int> OnCurrencyChanged;
        public event System.Action<int> OnWaveChanged;
        public event System.Action OnGameOver;
        public event System.Action<int> OnMapExpanded; // int = number of new pieces

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
            currentLives = startingLives;
            currentCurrency = startingCurrency;
            currentForkChance = baseForkChance;
            GenerateMap();

            // Subscribe to wave completion for map expansion
            waveManager = FindObjectOfType<WaveManager>();
            if (waveManager != null)
            {
                waveManager.OnWaveComplete += OnWaveComplete;
            }

            // Fire initial events after a frame to ensure all subscribers are ready
            Invoke(nameof(FireInitialEvents), 0.1f);
        }

        private void OnDestroy()
        {
            if (waveManager != null)
            {
                waveManager.OnWaveComplete -= OnWaveComplete;
            }
        }

        private void FireInitialEvents()
        {
            OnLivesChanged?.Invoke(currentLives);
            OnCurrencyChanged?.Invoke(currentCurrency);
            OnWaveChanged?.Invoke(currentWave);
        }

        private void GenerateMap()
        {
            mapGenerator = new MapGenerator();
            mapData = mapGenerator.Generate();
            spawnPoints = mapGenerator.GetSpawnPoints();

            foreach (var kvp in mapData)
            {
                CreateHexPiece(kvp.Value);
            }

            Debug.Log($"Map generated with {hexPieces.Count} pieces and {spawnPoints.Count} spawn points");
        }

        private void CreateHexPiece(HexPieceData data)
        {
            GameObject pieceObj;

            if (hexPiecePrefab != null)
            {
                pieceObj = Instantiate(hexPiecePrefab);
            }
            else
            {
                pieceObj = new GameObject($"HexPiece_{data.Coord}");
            }

            var piece = pieceObj.GetComponent<HexPiece>();
            if (piece == null)
            {
                piece = pieceObj.AddComponent<HexPiece>();
            }

            piece.Initialize(data, hexBaseMaterial, hexPathMaterial, castleMaterial);
            hexPieces[data.Coord] = piece;
        }

        public void AddCurrency(int amount)
        {
            currentCurrency += amount;
            OnCurrencyChanged?.Invoke(currentCurrency);
        }

        public bool SpendCurrency(int amount)
        {
            if (currentCurrency < amount) return false;
            currentCurrency -= amount;
            OnCurrencyChanged?.Invoke(currentCurrency);
            return true;
        }

        public void LoseLife()
        {
            currentLives--;
            OnLivesChanged?.Invoke(currentLives);

            if (currentLives <= 0)
            {
                gameOver = true;
                OnGameOver?.Invoke();
            }
        }

        public void StartNextWave()
        {
            if (gameOver) return;
            currentWave++;
            OnWaveChanged?.Invoke(currentWave);
        }

        private void OnWaveComplete()
        {
            Debug.Log("Wave complete! Expanding map...");
            ExpandMap();
        }

        private void ExpandMap()
        {
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("No spawn points to expand from");
                return;
            }

            // Pick a random dead end to expand from
            int randomIndex = Random.Range(0, spawnPoints.Count);
            HexCoord deadEndCoord = spawnPoints[randomIndex];

            Debug.Log($"Expanding from dead end at {deadEndCoord} with fork chance: {currentForkChance:P0}");

            // Generate new pieces with current fork chance
            var newPieces = mapGenerator.ExpandFromDeadEnd(mapData, deadEndCoord, 3, currentForkChance);

            // Update fork chance based on whether a fork was generated
            if (mapGenerator.ForkGeneratedLastExpansion)
            {
                // Reset fork chance when a fork is generated
                currentForkChance = baseForkChance;
                Debug.Log($"Fork generated! Resetting fork chance to {currentForkChance:P0}");
            }
            else
            {
                // Increase fork chance for next wave
                currentForkChance = Mathf.Min(currentForkChance + forkChanceIncreasePerWave, maxForkChance);
                Debug.Log($"No fork generated. Fork chance increased to {currentForkChance:P0}");
            }

            if (newPieces.Count == 0)
            {
                Debug.LogWarning("Failed to expand map - no new pieces generated");
                return;
            }

            // Update map data
            foreach (var pieceData in newPieces)
            {
                mapData[pieceData.Coord] = pieceData;
            }

            // The first piece is the converted dead end - we need to recreate it
            var convertedPiece = newPieces[0];
            if (hexPieces.TryGetValue(convertedPiece.Coord, out var existingPiece))
            {
                // Destroy old piece and create new one
                Destroy(existingPiece.gameObject);
                hexPieces.Remove(convertedPiece.Coord);
            }

            // Create all new hex pieces (including the converted one)
            foreach (var pieceData in newPieces)
            {
                if (!hexPieces.ContainsKey(pieceData.Coord))
                {
                    CreateHexPiece(pieceData);
                }
            }

            // Update spawn points - remove old dead end, find new ones
            spawnPoints.Clear();
            foreach (var kvp in mapData)
            {
                if (kvp.Value.IsSpawnPoint)
                {
                    spawnPoints.Add(kvp.Key);
                }
            }

            Debug.Log($"Map expanded! New pieces: {newPieces.Count}, Total spawn points: {spawnPoints.Count}");

            // Notify WaveManager to recalculate paths
            if (waveManager != null)
            {
                waveManager.RecalculatePaths();
            }

            // Pan camera to show new pieces
            var cameraController = FindObjectOfType<CameraController>();
            if (cameraController != null)
            {
                var newPositions = new List<Vector3>();
                foreach (var pieceData in newPieces)
                {
                    if (hexPieces.TryGetValue(pieceData.Coord, out var piece))
                    {
                        newPositions.Add(piece.transform.position);
                    }
                }

                if (newPositions.Count > 0)
                {
                    // Expand camera bounds to include new positions
                    cameraController.ExpandBoundsToInclude(newPositions);
                    // Pan camera toward new pieces (no zoom change)
                    cameraController.PanTowardPositions(newPositions);
                }
            }

            // Fire event for UI/other systems
            OnMapExpanded?.Invoke(newPieces.Count);
        }
    }
}