# Hexagonal Tower Defense - Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a mobile tower defense game with hexagonal map pieces, path-based enemy movement, and strategic tower placement.

**Architecture:** Component-based Unity architecture with clear separation between grid logic (pure C#), entities (MonoBehaviours), and UI. Map generates 7 hex pieces at startup with pre-computed pathfinding.

**Tech Stack:** Unity 2022.3, C#, Unity UI (Canvas), built-in mesh generation

---

## Task 1: Project Folder Structure

**Files:**
- Create: `Assets/Scripts/Core/` (folder)
- Create: `Assets/Scripts/Grid/` (folder)
- Create: `Assets/Scripts/Entities/` (folder)
- Create: `Assets/Scripts/UI/` (folder)
- Create: `Assets/Materials/` (folder)
- Create: `Assets/Prefabs/` (folder)

**Step 1: Create folder structure via Unity**

Use Unity MCP to create the required folders.

**Step 2: Verify folders exist**

Check that all folders are created in the Assets directory.

---

## Task 2: Hex Coordinate System (HexCoord)

**Files:**
- Create: `Assets/Scripts/Grid/HexCoord.cs`
- Create: `Assets/Scripts/Tests/HexCoordTests.cs`

**Step 1: Create HexCoord struct**

```csharp
using UnityEngine;

namespace TowerDefense.Grid
{
    [System.Serializable]
    public struct HexCoord : System.IEquatable<HexCoord>
    {
        public int q;
        public int r;

        public int S => -q - r;

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        // Flat-top hex: 6 directions starting from right (edge 0), counter-clockwise
        public static readonly HexCoord[] Directions = new HexCoord[]
        {
            new HexCoord(1, 0),   // Edge 0: Right
            new HexCoord(0, 1),   // Edge 1: Upper-right
            new HexCoord(-1, 1),  // Edge 2: Upper-left
            new HexCoord(-1, 0),  // Edge 3: Left
            new HexCoord(0, -1),  // Edge 4: Lower-left
            new HexCoord(1, -1),  // Edge 5: Lower-right
        };

        public HexCoord GetNeighbor(int edge)
        {
            edge = ((edge % 6) + 6) % 6;
            return this + Directions[edge];
        }

        public static int OppositeEdge(int edge)
        {
            return (edge + 3) % 6;
        }

        public static HexCoord operator +(HexCoord a, HexCoord b)
        {
            return new HexCoord(a.q + b.q, a.r + b.r);
        }

        public static HexCoord operator -(HexCoord a, HexCoord b)
        {
            return new HexCoord(a.q - b.q, a.r - b.r);
        }

        public static bool operator ==(HexCoord a, HexCoord b)
        {
            return a.q == b.q && a.r == b.r;
        }

        public static bool operator !=(HexCoord a, HexCoord b)
        {
            return !(a == b);
        }

        public bool Equals(HexCoord other)
        {
            return q == other.q && r == other.r;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            return q * 1000 + r;
        }

        public override string ToString()
        {
            return $"Hex({q}, {r})";
        }
    }
}
```

**Step 2: Create EditMode tests for HexCoord**

```csharp
using NUnit.Framework;
using TowerDefense.Grid;

namespace TowerDefense.Tests
{
    public class HexCoordTests
    {
        [Test]
        public void HexCoord_GetNeighbor_ReturnsCorrectNeighbor()
        {
            var center = new HexCoord(0, 0);

            Assert.AreEqual(new HexCoord(1, 0), center.GetNeighbor(0));  // Right
            Assert.AreEqual(new HexCoord(0, 1), center.GetNeighbor(1));  // Upper-right
            Assert.AreEqual(new HexCoord(-1, 1), center.GetNeighbor(2)); // Upper-left
            Assert.AreEqual(new HexCoord(-1, 0), center.GetNeighbor(3)); // Left
            Assert.AreEqual(new HexCoord(0, -1), center.GetNeighbor(4)); // Lower-left
            Assert.AreEqual(new HexCoord(1, -1), center.GetNeighbor(5)); // Lower-right
        }

        [Test]
        public void HexCoord_OppositeEdge_ReturnsCorrectOpposite()
        {
            Assert.AreEqual(3, HexCoord.OppositeEdge(0));
            Assert.AreEqual(4, HexCoord.OppositeEdge(1));
            Assert.AreEqual(5, HexCoord.OppositeEdge(2));
            Assert.AreEqual(0, HexCoord.OppositeEdge(3));
            Assert.AreEqual(1, HexCoord.OppositeEdge(4));
            Assert.AreEqual(2, HexCoord.OppositeEdge(5));
        }

        [Test]
        public void HexCoord_Equality_WorksCorrectly()
        {
            var a = new HexCoord(1, 2);
            var b = new HexCoord(1, 2);
            var c = new HexCoord(2, 1);

            Assert.IsTrue(a == b);
            Assert.IsFalse(a == c);
            Assert.IsTrue(a.Equals(b));
        }

        [Test]
        public void HexCoord_S_CalculatesCorrectly()
        {
            var coord = new HexCoord(1, 2);
            Assert.AreEqual(-3, coord.S);
        }
    }
}
```

**Step 3: Run tests**

Run Unity EditMode tests via `mcp__unityMCP__run_tests` with mode="EditMode".

---

## Task 3: Hex Grid Utilities (HexGrid)

**Files:**
- Create: `Assets/Scripts/Grid/HexGrid.cs`

**Step 1: Create HexGrid static utility class**

```csharp
using UnityEngine;

namespace TowerDefense.Grid
{
    public static class HexGrid
    {
        public const float OuterRadius = 5f;
        public static readonly float InnerRadius = OuterRadius * Mathf.Sqrt(3f) / 2f;

        // Flat-top hex layout
        public static Vector3 HexToWorld(HexCoord coord)
        {
            float x = OuterRadius * 1.5f * coord.q;
            float z = InnerRadius * 2f * (coord.r + coord.q * 0.5f);
            return new Vector3(x, 0f, z);
        }

        public static Vector3 GetEdgeMidpoint(HexCoord coord, int edge)
        {
            Vector3 center = HexToWorld(coord);
            float angle = 60f * edge;
            float rad = angle * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * InnerRadius;
        }

        public static Vector3[] GetHexCorners(HexCoord coord)
        {
            Vector3 center = HexToWorld(coord);
            Vector3[] corners = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = 60f * i - 30f; // Flat-top: first corner at -30 degrees
                float rad = angle * Mathf.Deg2Rad;
                corners[i] = center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * OuterRadius;
            }
            return corners;
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 4: Hex Piece Data (HexPieceData)

**Files:**
- Create: `Assets/Scripts/Grid/HexPieceType.cs`
- Create: `Assets/Scripts/Grid/HexPieceData.cs`

**Step 1: Create HexPieceType enum**

```csharp
namespace TowerDefense.Grid
{
    public enum HexPieceType
    {
        Castle,     // 1 connection, center piece
        Straight,   // 2 connections, opposite edges
        Bend,       // 2 connections, adjacent edges
        Fork,       // 3 connections, T-junction
        DeadEnd     // 1 connection, spawn point
    }
}
```

**Step 2: Create HexPieceData class**

```csharp
using System.Collections.Generic;

namespace TowerDefense.Grid
{
    [System.Serializable]
    public class HexPieceData
    {
        public HexCoord Coord;
        public HexPieceType Type;
        public List<int> ConnectedEdges;
        public bool IsSpawnPoint => Type == HexPieceType.DeadEnd;
        public bool IsCastle => Type == HexPieceType.Castle;

        public HexPieceData(HexCoord coord, HexPieceType type, List<int> connectedEdges)
        {
            Coord = coord;
            Type = type;
            ConnectedEdges = connectedEdges;
        }
    }
}
```

**Step 3: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 5: Map Generator Logic

**Files:**
- Create: `Assets/Scripts/Grid/MapGenerator.cs`

**Step 1: Create MapGenerator class**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Grid
{
    public class MapGenerator
    {
        private Dictionary<HexCoord, HexPieceData> pieces = new Dictionary<HexCoord, HexPieceData>();
        private System.Random random;

        public MapGenerator(int seed = -1)
        {
            random = seed >= 0 ? new System.Random(seed) : new System.Random();
        }

        public Dictionary<HexCoord, HexPieceData> Generate()
        {
            pieces.Clear();

            // Step 1: Place castle at center with random exit edge
            int castleExitEdge = random.Next(6);
            var castle = new HexPieceData(
                new HexCoord(0, 0),
                HexPieceType.Castle,
                new List<int> { castleExitEdge }
            );
            pieces[castle.Coord] = castle;

            // Step 2: Generate path from castle
            GeneratePath(castle.Coord, castleExitEdge, 6);

            return new Dictionary<HexCoord, HexPieceData>(pieces);
        }

        private void GeneratePath(HexCoord fromCoord, int exitEdge, int remainingPieces)
        {
            if (remainingPieces <= 0) return;

            HexCoord newCoord = fromCoord.GetNeighbor(exitEdge);
            int entryEdge = HexCoord.OppositeEdge(exitEdge);

            // Check for collision
            if (pieces.ContainsKey(newCoord))
            {
                // Force dead-end at previous piece
                return;
            }

            // Last piece is always a dead-end
            if (remainingPieces == 1)
            {
                var deadEnd = new HexPieceData(newCoord, HexPieceType.DeadEnd, new List<int> { entryEdge });
                pieces[newCoord] = deadEnd;
                return;
            }

            // Decide piece type: 60% straight, 30% bend, 10% fork
            float roll = (float)random.NextDouble();
            HexPieceType type;
            List<int> edges = new List<int> { entryEdge };

            if (roll < 0.6f)
            {
                type = HexPieceType.Straight;
                int newExitEdge = HexCoord.OppositeEdge(entryEdge);
                edges.Add(newExitEdge);

                var piece = new HexPieceData(newCoord, type, edges);
                pieces[newCoord] = piece;

                GeneratePath(newCoord, newExitEdge, remainingPieces - 1);
            }
            else if (roll < 0.9f)
            {
                type = HexPieceType.Bend;
                // Pick adjacent edge (not opposite)
                int offset = random.Next(2) == 0 ? 1 : -1;
                int newExitEdge = ((entryEdge + 3 + offset) % 6 + 6) % 6;
                edges.Add(newExitEdge);

                var piece = new HexPieceData(newCoord, type, edges);
                pieces[newCoord] = piece;

                GeneratePath(newCoord, newExitEdge, remainingPieces - 1);
            }
            else
            {
                type = HexPieceType.Fork;
                int exitEdge1 = HexCoord.OppositeEdge(entryEdge);
                int exitEdge2 = ((entryEdge + 2) % 6 + 6) % 6;
                edges.Add(exitEdge1);
                edges.Add(exitEdge2);

                var piece = new HexPieceData(newCoord, type, edges);
                pieces[newCoord] = piece;

                // Main path continues
                int mainPieces = remainingPieces / 2;
                int branchPieces = remainingPieces - mainPieces - 1;

                GeneratePath(newCoord, exitEdge1, mainPieces);
                GeneratePath(newCoord, exitEdge2, branchPieces > 0 ? branchPieces : 1);
            }
        }

        public List<HexCoord> GetSpawnPoints()
        {
            var spawnPoints = new List<HexCoord>();
            foreach (var piece in pieces.Values)
            {
                if (piece.IsSpawnPoint)
                {
                    spawnPoints.Add(piece.Coord);
                }
            }
            return spawnPoints;
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 6: Hex Mesh Generator

**Files:**
- Create: `Assets/Scripts/Grid/HexMeshGenerator.cs`

**Step 1: Create HexMeshGenerator class**

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace TowerDefense.Grid
{
    public static class HexMeshGenerator
    {
        public static Mesh CreateHexMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "HexMesh";

            Vector3[] vertices = new Vector3[7]; // Center + 6 corners
            int[] triangles = new int[18]; // 6 triangles * 3 vertices

            vertices[0] = Vector3.zero; // Center

            for (int i = 0; i < 6; i++)
            {
                float angle = 60f * i - 30f; // Flat-top
                float rad = angle * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(rad) * HexGrid.OuterRadius,
                    0f,
                    Mathf.Sin(rad) * HexGrid.OuterRadius
                );
            }

            for (int i = 0; i < 6; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % 6 + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        public static Mesh CreatePathMesh(List<int> connectedEdges)
        {
            if (connectedEdges == null || connectedEdges.Count == 0)
                return null;

            Mesh mesh = new Mesh();
            mesh.name = "PathMesh";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            const float pathWidth = 1.5f;
            const float halfWidth = pathWidth / 2f;

            // For each edge, create a path from center to edge midpoint
            foreach (int edge in connectedEdges)
            {
                float angle = 60f * edge;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x);

                Vector3 edgeMidpoint = direction * HexGrid.InnerRadius;

                int startIndex = vertices.Count;

                // Quad from center to edge
                vertices.Add(perpendicular * halfWidth);                    // Center left
                vertices.Add(-perpendicular * halfWidth);                   // Center right
                vertices.Add(edgeMidpoint + perpendicular * halfWidth);     // Edge left
                vertices.Add(edgeMidpoint - perpendicular * halfWidth);     // Edge right

                // Two triangles for the quad
                triangles.Add(startIndex);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 1);

                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 3);
            }

            // Add center circle if multiple paths
            if (connectedEdges.Count > 1)
            {
                int centerStart = vertices.Count;
                int segments = 16;
                vertices.Add(Vector3.zero); // Center point

                for (int i = 0; i < segments; i++)
                {
                    float angle = (360f / segments) * i * Mathf.Deg2Rad;
                    vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * halfWidth * 1.2f);
                }

                for (int i = 0; i < segments; i++)
                {
                    triangles.Add(centerStart);
                    triangles.Add(centerStart + 1 + i);
                    triangles.Add(centerStart + 1 + (i + 1) % segments);
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 7: Basic Materials

**Files:**
- Create: `Assets/Materials/HexBase.mat`
- Create: `Assets/Materials/HexPath.mat`
- Create: `Assets/Materials/Castle.mat`

**Step 1: Create materials via Unity MCP**

Create three Unlit/Color materials:
- HexBase: Light gray (0.8, 0.8, 0.8)
- HexPath: Dark gray (0.3, 0.3, 0.3)
- Castle: Gold/yellow (0.9, 0.7, 0.2)

---

## Task 8: HexPiece MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Grid/HexPiece.cs`

**Step 1: Create HexPiece MonoBehaviour**

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace TowerDefense.Grid
{
    public class HexPiece : MonoBehaviour
    {
        [SerializeField] private MeshFilter hexMeshFilter;
        [SerializeField] private MeshRenderer hexRenderer;
        [SerializeField] private MeshFilter pathMeshFilter;
        [SerializeField] private MeshRenderer pathRenderer;

        private HexPieceData data;
        private List<TowerSlot> slots = new List<TowerSlot>();

        public HexPieceData Data => data;
        public List<TowerSlot> Slots => slots;

        public void Initialize(HexPieceData pieceData, Material hexMaterial, Material pathMaterial, Material castleMaterial)
        {
            data = pieceData;
            transform.position = HexGrid.HexToWorld(data.Coord);

            // Create hex base mesh
            if (hexMeshFilter != null)
            {
                hexMeshFilter.mesh = HexMeshGenerator.CreateHexMesh();
            }

            // Set material based on type
            if (hexRenderer != null)
            {
                hexRenderer.material = data.IsCastle ? castleMaterial : hexMaterial;
            }

            // Create path mesh
            if (pathMeshFilter != null && data.ConnectedEdges.Count > 0)
            {
                pathMeshFilter.mesh = HexMeshGenerator.CreatePathMesh(data.ConnectedEdges);
            }

            if (pathRenderer != null)
            {
                pathRenderer.material = pathMaterial;
            }

            // Generate tower slots
            GenerateSlots();
        }

        private void GenerateSlots()
        {
            const float slotDistance = 1.2f;
            const float slotOffsetAlongPath = 2.5f;

            foreach (int edge in data.ConnectedEdges)
            {
                float angle = 60f * edge;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x);

                Vector3 pathPoint = direction * slotOffsetAlongPath;

                // Create slot on each side of the path
                CreateSlot(transform.position + pathPoint + perpendicular * slotDistance);
                CreateSlot(transform.position + pathPoint - perpendicular * slotDistance);
            }
        }

        private void CreateSlot(Vector3 position)
        {
            GameObject slotObj = new GameObject("TowerSlot");
            slotObj.transform.SetParent(transform);
            slotObj.transform.position = position;

            TowerSlot slot = slotObj.AddComponent<TowerSlot>();
            slot.Initialize(this);
            slots.Add(slot);
        }

        public Vector3 GetEdgeWorldPosition(int edge)
        {
            float angle = 60f * edge;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            return transform.position + direction * HexGrid.InnerRadius;
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 9: TowerSlot Component

**Files:**
- Create: `Assets/Scripts/Entities/TowerSlot.cs`

**Step 1: Create TowerSlot MonoBehaviour**

```csharp
using UnityEngine;

namespace TowerDefense.Grid
{
    public class TowerSlot : MonoBehaviour
    {
        private HexPiece parentHex;
        private Tower currentTower;
        private GameObject visualIndicator;

        public bool IsOccupied => currentTower != null;
        public HexPiece ParentHex => parentHex;
        public Tower CurrentTower => currentTower;

        public void Initialize(HexPiece hex)
        {
            parentHex = hex;
            CreateVisualIndicator();
        }

        private void CreateVisualIndicator()
        {
            visualIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualIndicator.transform.SetParent(transform);
            visualIndicator.transform.localPosition = Vector3.zero;
            visualIndicator.transform.localScale = new Vector3(1f, 0.1f, 1f);

            // Remove collider from primitive
            var collider = visualIndicator.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            // Set color
            var renderer = visualIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Unlit/Color"));
                renderer.material.color = new Color(0.2f, 0.6f, 0.2f, 0.5f);
            }

            // Add collider to slot for click detection
            var slotCollider = gameObject.AddComponent<SphereCollider>();
            slotCollider.radius = 0.6f;
        }

        public bool PlaceTower(Tower tower)
        {
            if (IsOccupied) return false;

            currentTower = tower;
            tower.transform.position = transform.position;
            tower.transform.SetParent(transform);
            visualIndicator.SetActive(false);

            return true;
        }

        public Tower RemoveTower()
        {
            if (!IsOccupied) return null;

            Tower tower = currentTower;
            currentTower = null;
            visualIndicator.SetActive(true);

            return tower;
        }

        public void SetHighlight(bool highlighted)
        {
            if (visualIndicator != null)
            {
                var renderer = visualIndicator.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = highlighted
                        ? new Color(0.2f, 0.9f, 0.2f, 0.8f)
                        : new Color(0.2f, 0.6f, 0.2f, 0.5f);
                }
            }
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 10: GameManager Setup

**Files:**
- Create: `Assets/Scripts/Core/GameManager.cs`

**Step 1: Create GameManager singleton**

```csharp
using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Grid;

namespace TowerDefense.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int startingLives = 10;
        [SerializeField] private int startingCurrency = 200;

        [Header("Materials")]
        [SerializeField] private Material hexBaseMaterial;
        [SerializeField] private Material hexPathMaterial;
        [SerializeField] private Material castleMaterial;

        [Header("Prefabs")]
        [SerializeField] private GameObject hexPiecePrefab;

        private Dictionary<HexCoord, HexPiece> hexPieces = new Dictionary<HexCoord, HexPiece>();
        private List<HexCoord> spawnPoints = new List<HexCoord>();

        private int currentLives;
        private int currentCurrency;
        private int currentWave = 0;
        private bool gameOver = false;

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
            GenerateMap();
        }

        private void GenerateMap()
        {
            var generator = new MapGenerator();
            var mapData = generator.Generate();
            spawnPoints = generator.GetSpawnPoints();

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

                // Create hex base
                GameObject hexBase = new GameObject("HexBase");
                hexBase.transform.SetParent(pieceObj.transform);
                hexBase.AddComponent<MeshFilter>();
                hexBase.AddComponent<MeshRenderer>();

                // Create path
                GameObject pathObj = new GameObject("Path");
                pathObj.transform.SetParent(pieceObj.transform);
                pathObj.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                pathObj.AddComponent<MeshFilter>();
                pathObj.AddComponent<MeshRenderer>();

                var hexPiece = pieceObj.AddComponent<HexPiece>();

                // Use reflection or SerializedObject to set private fields
                // For now, we'll modify HexPiece to accept these in Initialize
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
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 11: Update HexPiece for Runtime Creation

**Files:**
- Modify: `Assets/Scripts/Grid/HexPiece.cs`

**Step 1: Update Initialize method to create child objects if needed**

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace TowerDefense.Grid
{
    public class HexPiece : MonoBehaviour
    {
        [SerializeField] private MeshFilter hexMeshFilter;
        [SerializeField] private MeshRenderer hexRenderer;
        [SerializeField] private MeshFilter pathMeshFilter;
        [SerializeField] private MeshRenderer pathRenderer;

        private HexPieceData data;
        private List<TowerSlot> slots = new List<TowerSlot>();

        public HexPieceData Data => data;
        public List<TowerSlot> Slots => slots;

        public void Initialize(HexPieceData pieceData, Material hexMaterial, Material pathMaterial, Material castleMaterial)
        {
            data = pieceData;
            transform.position = HexGrid.HexToWorld(data.Coord);
            gameObject.name = $"HexPiece_{data.Coord}_{data.Type}";

            // Create hex base if not assigned
            if (hexMeshFilter == null)
            {
                GameObject hexBase = new GameObject("HexBase");
                hexBase.transform.SetParent(transform);
                hexBase.transform.localPosition = Vector3.zero;
                hexMeshFilter = hexBase.AddComponent<MeshFilter>();
                hexRenderer = hexBase.AddComponent<MeshRenderer>();
            }

            hexMeshFilter.mesh = HexMeshGenerator.CreateHexMesh();
            hexRenderer.material = data.IsCastle ? castleMaterial : hexMaterial;

            // Create path if needed
            if (data.ConnectedEdges.Count > 0)
            {
                if (pathMeshFilter == null)
                {
                    GameObject pathObj = new GameObject("Path");
                    pathObj.transform.SetParent(transform);
                    pathObj.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                    pathMeshFilter = pathObj.AddComponent<MeshFilter>();
                    pathRenderer = pathObj.AddComponent<MeshRenderer>();
                }

                pathMeshFilter.mesh = HexMeshGenerator.CreatePathMesh(data.ConnectedEdges);
                if (pathRenderer != null && pathMaterial != null)
                {
                    pathRenderer.material = pathMaterial;
                }
            }

            // Create castle visual if castle type
            if (data.IsCastle)
            {
                CreateCastleVisual();
            }

            // Generate tower slots (skip for castle)
            if (!data.IsCastle)
            {
                GenerateSlots();
            }
        }

        private void CreateCastleVisual()
        {
            GameObject castle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            castle.name = "CastleModel";
            castle.transform.SetParent(transform);
            castle.transform.localPosition = new Vector3(0f, 1f, 0f);
            castle.transform.localScale = new Vector3(2f, 2f, 2f);

            var collider = castle.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private void GenerateSlots()
        {
            const float slotDistance = 1.2f;
            const float slotOffsetAlongPath = 2.5f;

            foreach (int edge in data.ConnectedEdges)
            {
                float angle = 60f * edge;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x);

                Vector3 pathPoint = direction * slotOffsetAlongPath;

                CreateSlot(transform.position + pathPoint + perpendicular * slotDistance);
                CreateSlot(transform.position + pathPoint - perpendicular * slotDistance);
            }
        }

        private void CreateSlot(Vector3 position)
        {
            GameObject slotObj = new GameObject("TowerSlot");
            slotObj.transform.SetParent(transform);
            slotObj.transform.position = position;

            TowerSlot slot = slotObj.AddComponent<TowerSlot>();
            slot.Initialize(this);
            slots.Add(slot);
        }

        public Vector3 GetEdgeWorldPosition(int edge)
        {
            float angle = 60f * edge;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            return transform.position + direction * HexGrid.InnerRadius;
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 12: Pathfinding System

**Files:**
- Create: `Assets/Scripts/Grid/PathFinder.cs`

**Step 1: Create PathFinder for enemy navigation**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Grid
{
    public class PathFinder
    {
        private Dictionary<HexCoord, HexPieceData> map;

        public PathFinder(Dictionary<HexCoord, HexPieceData> mapData)
        {
            map = mapData;
        }

        public List<Vector3> FindPathToCastle(HexCoord start)
        {
            var hexPath = BFSTocastle(start);
            if (hexPath == null || hexPath.Count == 0)
            {
                Debug.LogWarning($"No path found from {start} to castle");
                return new List<Vector3>();
            }

            return ConvertToWaypoints(hexPath);
        }

        private List<HexCoord> BFSTocastle(HexCoord start)
        {
            var queue = new Queue<HexCoord>();
            var visited = new HashSet<HexCoord>();
            var cameFrom = new Dictionary<HexCoord, HexCoord>();

            queue.Enqueue(start);
            visited.Add(start);

            HexCoord? castleCoord = null;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (map.TryGetValue(current, out var piece) && piece.IsCastle)
                {
                    castleCoord = current;
                    break;
                }

                if (!map.TryGetValue(current, out var currentPiece))
                    continue;

                foreach (int edge in currentPiece.ConnectedEdges)
                {
                    var neighbor = current.GetNeighbor(edge);
                    if (visited.Contains(neighbor))
                        continue;

                    if (!map.TryGetValue(neighbor, out var neighborPiece))
                        continue;

                    int requiredEdge = HexCoord.OppositeEdge(edge);
                    if (!neighborPiece.ConnectedEdges.Contains(requiredEdge))
                        continue;

                    visited.Add(neighbor);
                    cameFrom[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }

            if (!castleCoord.HasValue)
                return null;

            var path = new List<HexCoord>();
            var node = castleCoord.Value;
            while (cameFrom.ContainsKey(node))
            {
                path.Add(node);
                node = cameFrom[node];
            }
            path.Add(start);
            path.Reverse();

            return path;
        }

        private List<Vector3> ConvertToWaypoints(List<HexCoord> hexPath)
        {
            var waypoints = new List<Vector3>();

            for (int i = 0; i < hexPath.Count; i++)
            {
                var coord = hexPath[i];
                var worldPos = HexGrid.HexToWorld(coord);

                if (i == 0)
                {
                    // Start at spawn point center
                    waypoints.Add(worldPos);
                }
                else
                {
                    // Add entry point, then center
                    var prevCoord = hexPath[i - 1];
                    int entryEdge = GetConnectingEdge(prevCoord, coord);
                    if (entryEdge >= 0)
                    {
                        waypoints.Add(GetEdgeMidpoint(coord, entryEdge));
                    }
                    waypoints.Add(worldPos);
                }
            }

            return waypoints;
        }

        private int GetConnectingEdge(HexCoord from, HexCoord to)
        {
            for (int i = 0; i < 6; i++)
            {
                if (from.GetNeighbor(i) == to)
                    return HexCoord.OppositeEdge(i);
            }
            return -1;
        }

        private Vector3 GetEdgeMidpoint(HexCoord coord, int edge)
        {
            Vector3 center = HexGrid.HexToWorld(coord);
            float angle = 60f * edge;
            float rad = angle * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * HexGrid.InnerRadius;
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 13: Enemy Component

**Files:**
- Create: `Assets/Scripts/Entities/Enemy.cs`

**Step 1: Create Enemy MonoBehaviour**

```csharp
using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Core;

namespace TowerDefense.Entities
{
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private float baseSpeed = 2f;
        [SerializeField] private float baseHealth = 10f;
        [SerializeField] private int currencyReward = 10;

        private List<Vector3> waypoints;
        private int currentWaypointIndex;
        private float currentHealth;
        private float currentSpeed;
        private float speedMultiplier = 1f;
        private float slowTimer;

        public float Health => currentHealth;
        public float MaxHealth => baseHealth;
        public bool IsDead => currentHealth <= 0;

        public event System.Action<Enemy> OnDeath;
        public event System.Action<Enemy> OnReachedCastle;

        public void Initialize(List<Vector3> path, int waveNumber)
        {
            waypoints = path;
            currentWaypointIndex = 0;
            currentHealth = baseHealth + (waveNumber - 1) * 5f;
            currentSpeed = baseSpeed + (waveNumber - 1) * 0.1f;
            speedMultiplier = 1f;

            if (waypoints.Count > 0)
            {
                transform.position = waypoints[0];
            }

            CreateVisual();
        }

        private void CreateVisual()
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.transform.SetParent(transform);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            var collider = visual.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Unlit/Color"));
                renderer.material.color = Color.red;
            }

            // Add collider to enemy for targeting
            var sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.4f;
            sphereCollider.center = new Vector3(0f, 0.5f, 0f);
        }

        private void Update()
        {
            if (IsDead || waypoints == null || waypoints.Count == 0)
                return;

            UpdateSlowEffect();
            MoveAlongPath();
        }

        private void UpdateSlowEffect()
        {
            if (slowTimer > 0)
            {
                slowTimer -= Time.deltaTime;
                if (slowTimer <= 0)
                {
                    speedMultiplier = 1f;
                }
            }
        }

        private void MoveAlongPath()
        {
            if (currentWaypointIndex >= waypoints.Count)
            {
                ReachCastle();
                return;
            }

            Vector3 target = waypoints[currentWaypointIndex];
            Vector3 direction = (target - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, target);
            float moveDistance = currentSpeed * speedMultiplier * Time.deltaTime;

            if (moveDistance >= distance)
            {
                transform.position = target;
                currentWaypointIndex++;
            }
            else
            {
                transform.position += direction * moveDistance;

                // Face movement direction
                if (direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }
        }

        public void TakeDamage(float damage)
        {
            currentHealth -= damage;

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        public void ApplySlow(float multiplier, float duration)
        {
            speedMultiplier = multiplier;
            slowTimer = duration;
        }

        private void Die()
        {
            GameManager.Instance?.AddCurrency(currencyReward);
            OnDeath?.Invoke(this);
            Destroy(gameObject);
        }

        private void ReachCastle()
        {
            GameManager.Instance?.LoseLife();
            OnReachedCastle?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 14: Tower Data

**Files:**
- Create: `Assets/Scripts/Entities/TowerData.cs`

**Step 1: Create TowerData ScriptableObject**

```csharp
using UnityEngine;

namespace TowerDefense.Entities
{
    [CreateAssetMenu(fileName = "TowerData", menuName = "Tower Defense/Tower Data")]
    public class TowerData : ScriptableObject
    {
        public string towerName;
        public int cost;
        public float damage;
        public float range;
        public float fireRate;
        public bool isAreaDamage;
        public float areaRadius;
        public bool appliesSlow;
        public float slowMultiplier;
        public float slowDuration;
        public Color towerColor = Color.blue;
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 15: Tower Component

**Files:**
- Create: `Assets/Scripts/Entities/Tower.cs`

**Step 1: Create Tower MonoBehaviour**

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace TowerDefense.Entities
{
    public class Tower : MonoBehaviour
    {
        private TowerData data;
        private float fireCooldown;
        private Enemy currentTarget;
        private GameObject rangeIndicator;
        private Transform turretHead;

        public TowerData Data => data;
        public int SellValue => data != null ? data.cost / 2 : 0;

        public void Initialize(TowerData towerData)
        {
            data = towerData;
            fireCooldown = 0f;
            CreateVisual();
        }

        private void CreateVisual()
        {
            // Base
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.transform.SetParent(transform);
            baseObj.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            baseObj.transform.localScale = new Vector3(0.8f, 0.25f, 0.8f);

            var baseCollider = baseObj.GetComponent<Collider>();
            if (baseCollider != null) Destroy(baseCollider);

            var baseRenderer = baseObj.GetComponent<Renderer>();
            if (baseRenderer != null)
            {
                baseRenderer.material = new Material(Shader.Find("Unlit/Color"));
                baseRenderer.material.color = data != null ? data.towerColor : Color.blue;
            }

            // Turret head
            var headObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headObj.name = "TurretHead";
            headObj.transform.SetParent(transform);
            headObj.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            headObj.transform.localScale = new Vector3(0.4f, 0.4f, 0.6f);
            turretHead = headObj.transform;

            var headCollider = headObj.GetComponent<Collider>();
            if (headCollider != null) Destroy(headCollider);

            var headRenderer = headObj.GetComponent<Renderer>();
            if (headRenderer != null)
            {
                headRenderer.material = new Material(Shader.Find("Unlit/Color"));
                headRenderer.material.color = data != null ? data.towerColor * 0.7f : Color.blue * 0.7f;
            }

            // Range indicator (hidden by default)
            CreateRangeIndicator();
        }

        private void CreateRangeIndicator()
        {
            rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rangeIndicator.transform.SetParent(transform);
            rangeIndicator.transform.localPosition = Vector3.zero;

            float range = data != null ? data.range : 3f;
            rangeIndicator.transform.localScale = new Vector3(range * 2f, 0.01f, range * 2f);

            var collider = rangeIndicator.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = rangeIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Unlit/Color"));
                renderer.material.color = new Color(1f, 1f, 0f, 0.2f);
            }

            rangeIndicator.SetActive(false);
        }

        public void ShowRange(bool show)
        {
            if (rangeIndicator != null)
            {
                rangeIndicator.SetActive(show);
            }
        }

        private void Update()
        {
            if (data == null) return;

            fireCooldown -= Time.deltaTime;

            FindTarget();

            if (currentTarget != null)
            {
                RotateTowardsTarget();

                if (fireCooldown <= 0f)
                {
                    Fire();
                    fireCooldown = 1f / data.fireRate;
                }
            }
        }

        private void FindTarget()
        {
            if (currentTarget != null)
            {
                float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
                if (currentTarget.IsDead || distance > data.range)
                {
                    currentTarget = null;
                }
            }

            if (currentTarget == null)
            {
                currentTarget = FindClosestEnemy();
            }
        }

        private Enemy FindClosestEnemy()
        {
            var enemies = FindObjectsOfType<Enemy>();
            Enemy closest = null;
            float closestDistance = data.range;

            foreach (var enemy in enemies)
            {
                if (enemy.IsDead) continue;

                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance <= closestDistance)
                {
                    closest = enemy;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private void RotateTowardsTarget()
        {
            if (turretHead == null || currentTarget == null) return;

            Vector3 direction = currentTarget.transform.position - transform.position;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                turretHead.rotation = Quaternion.Slerp(turretHead.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }

        private void Fire()
        {
            if (currentTarget == null) return;

            if (data.appliesSlow)
            {
                // Slow tower - instant effect
                currentTarget.ApplySlow(data.slowMultiplier, data.slowDuration);
            }
            else if (data.isAreaDamage)
            {
                // Area damage
                var enemies = FindObjectsOfType<Enemy>();
                foreach (var enemy in enemies)
                {
                    float distance = Vector3.Distance(currentTarget.transform.position, enemy.transform.position);
                    if (distance <= data.areaRadius)
                    {
                        enemy.TakeDamage(data.damage);
                    }
                }
            }
            else
            {
                // Single target damage
                currentTarget.TakeDamage(data.damage);
            }

            // Visual feedback - simple line
            Debug.DrawLine(turretHead.position, currentTarget.transform.position, Color.yellow, 0.1f);
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 16: Wave Manager

**Files:**
- Create: `Assets/Scripts/Core/WaveManager.cs`

**Step 1: Create WaveManager component**

```csharp
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TowerDefense.Grid;
using TowerDefense.Entities;

namespace TowerDefense.Core
{
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private float timeBetweenWaves = 20f;
        [SerializeField] private float timeBetweenSpawns = 0.5f;
        [SerializeField] private int baseEnemiesPerWave = 5;

        private Dictionary<HexCoord, List<Vector3>> spawnPaths = new Dictionary<HexCoord, List<Vector3>>();
        private List<Enemy> activeEnemies = new List<Enemy>();
        private bool waveInProgress;
        private int currentWave;

        public bool WaveInProgress => waveInProgress;
        public int EnemyCount => activeEnemies.Count;

        public event System.Action OnWaveComplete;

        private void Start()
        {
            StartCoroutine(InitializePaths());
        }

        private IEnumerator InitializePaths()
        {
            // Wait for GameManager to generate map
            yield return new WaitForSeconds(0.1f);

            var gameManager = GameManager.Instance;
            if (gameManager == null) yield break;

            var mapData = new Dictionary<HexCoord, HexPieceData>();
            foreach (var kvp in gameManager.HexPieces)
            {
                mapData[kvp.Key] = kvp.Value.Data;
            }

            var pathFinder = new PathFinder(mapData);

            foreach (var spawnPoint in gameManager.SpawnPoints)
            {
                var path = pathFinder.FindPathToCastle(spawnPoint);
                if (path.Count > 0)
                {
                    spawnPaths[spawnPoint] = path;
                    Debug.Log($"Path from {spawnPoint} has {path.Count} waypoints");
                }
            }
        }

        public void StartWave()
        {
            if (waveInProgress) return;

            currentWave++;
            GameManager.Instance?.StartNextWave();
            StartCoroutine(SpawnWave());
        }

        private IEnumerator SpawnWave()
        {
            waveInProgress = true;
            int enemiesPerSpawn = baseEnemiesPerWave + (currentWave - 1) * 2;

            foreach (var kvp in spawnPaths)
            {
                for (int i = 0; i < enemiesPerSpawn; i++)
                {
                    SpawnEnemy(kvp.Value);
                    yield return new WaitForSeconds(timeBetweenSpawns);
                }
            }

            // Wait for all enemies to be defeated
            while (activeEnemies.Count > 0)
            {
                activeEnemies.RemoveAll(e => e == null);
                yield return new WaitForSeconds(0.5f);
            }

            waveInProgress = false;
            GameManager.Instance?.AddCurrency(50); // Wave bonus
            OnWaveComplete?.Invoke();
        }

        private void SpawnEnemy(List<Vector3> path)
        {
            GameObject enemyObj = new GameObject("Enemy");
            var enemy = enemyObj.AddComponent<Enemy>();
            enemy.Initialize(new List<Vector3>(path), currentWave);

            enemy.OnDeath += HandleEnemyDeath;
            enemy.OnReachedCastle += HandleEnemyReachedCastle;

            activeEnemies.Add(enemy);
        }

        private void HandleEnemyDeath(Enemy enemy)
        {
            activeEnemies.Remove(enemy);
        }

        private void HandleEnemyReachedCastle(Enemy enemy)
        {
            activeEnemies.Remove(enemy);
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 17: Camera Controller

**Files:**
- Create: `Assets/Scripts/UI/CameraController.cs`

**Step 1: Create CameraController for mobile**

```csharp
using UnityEngine;

namespace TowerDefense.UI
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float minZoom = 10f;
        [SerializeField] private float maxZoom = 40f;
        [SerializeField] private float panSpeed = 0.5f;
        [SerializeField] private float zoomSpeed = 0.1f;
        [SerializeField] private float panBounds = 30f;

        private Camera cam;
        private Vector3 lastPanPosition;
        private float lastPinchDistance;
        private bool isPanning;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                cam = Camera.main;
            }
        }

        private void Start()
        {
            // Set up orthographic top-down view
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = (minZoom + maxZoom) / 2f;
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                transform.position = new Vector3(0f, 50f, 0f);
            }
        }

        private void Update()
        {
            HandleTouchInput();
            HandleMouseInput(); // For editor testing
        }

        private void HandleTouchInput()
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    lastPanPosition = touch.position;
                    isPanning = true;
                }
                else if (touch.phase == TouchPhase.Moved && isPanning)
                {
                    Vector3 delta = (Vector3)touch.position - lastPanPosition;
                    Pan(-delta * panSpeed * Time.deltaTime);
                    lastPanPosition = touch.position;
                }
                else if (touch.phase == TouchPhase.Ended)
                {
                    isPanning = false;
                }
            }
            else if (Input.touchCount == 2)
            {
                isPanning = false;

                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                float currentDistance = Vector2.Distance(touch0.position, touch1.position);

                if (touch1.phase == TouchPhase.Began)
                {
                    lastPinchDistance = currentDistance;
                }
                else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
                {
                    float delta = currentDistance - lastPinchDistance;
                    Zoom(-delta * zoomSpeed);
                    lastPinchDistance = currentDistance;
                }
            }
        }

        private void HandleMouseInput()
        {
            // Middle mouse drag for pan
            if (Input.GetMouseButtonDown(2))
            {
                lastPanPosition = Input.mousePosition;
                isPanning = true;
            }
            else if (Input.GetMouseButton(2) && isPanning)
            {
                Vector3 delta = Input.mousePosition - lastPanPosition;
                Pan(-delta * panSpeed * 0.1f);
                lastPanPosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(2))
            {
                isPanning = false;
            }

            // Scroll wheel for zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                Zoom(-scroll * 10f);
            }
        }

        private void Pan(Vector3 delta)
        {
            Vector3 move = new Vector3(delta.x, 0f, delta.y);
            Vector3 newPosition = transform.position + move;

            newPosition.x = Mathf.Clamp(newPosition.x, -panBounds, panBounds);
            newPosition.z = Mathf.Clamp(newPosition.z, -panBounds, panBounds);

            transform.position = newPosition;
        }

        private void Zoom(float delta)
        {
            if (cam == null) return;

            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + delta, minZoom, maxZoom);
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 18: Tower Manager (Building/Selling)

**Files:**
- Create: `Assets/Scripts/Core/TowerManager.cs`

**Step 1: Create TowerManager component**

```csharp
using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Entities;
using TowerDefense.Grid;

namespace TowerDefense.Core
{
    public class TowerManager : MonoBehaviour
    {
        [SerializeField] private List<TowerData> availableTowers;

        private TowerSlot selectedSlot;
        private Tower selectedTower;

        public TowerSlot SelectedSlot => selectedSlot;
        public Tower SelectedTower => selectedTower;
        public List<TowerData> AvailableTowers => availableTowers;

        public event System.Action<TowerSlot> OnSlotSelected;
        public event System.Action<Tower> OnTowerSelected;
        public event System.Action OnSelectionCleared;

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    // Check if we hit a tower slot
                    var slot = hit.collider.GetComponent<TowerSlot>();
                    if (slot != null)
                    {
                        SelectSlot(slot);
                        return;
                    }

                    // Check if we hit a tower (through its slot)
                    var slotParent = hit.collider.GetComponentInParent<TowerSlot>();
                    if (slotParent != null && slotParent.IsOccupied)
                    {
                        SelectTower(slotParent.CurrentTower);
                        return;
                    }
                }

                ClearSelection();
            }
        }

        public void SelectSlot(TowerSlot slot)
        {
            ClearSelection();

            if (slot.IsOccupied)
            {
                SelectTower(slot.CurrentTower);
                return;
            }

            selectedSlot = slot;
            slot.SetHighlight(true);
            OnSlotSelected?.Invoke(slot);
        }

        public void SelectTower(Tower tower)
        {
            ClearSelection();
            selectedTower = tower;
            tower.ShowRange(true);
            OnTowerSelected?.Invoke(tower);
        }

        public void ClearSelection()
        {
            if (selectedSlot != null)
            {
                selectedSlot.SetHighlight(false);
                selectedSlot = null;
            }

            if (selectedTower != null)
            {
                selectedTower.ShowRange(false);
                selectedTower = null;
            }

            OnSelectionCleared?.Invoke();
        }

        public bool BuildTower(TowerData towerData)
        {
            if (selectedSlot == null || selectedSlot.IsOccupied)
                return false;

            if (!GameManager.Instance.SpendCurrency(towerData.cost))
                return false;

            GameObject towerObj = new GameObject($"Tower_{towerData.towerName}");
            var tower = towerObj.AddComponent<Tower>();
            tower.Initialize(towerData);

            selectedSlot.PlaceTower(tower);
            ClearSelection();

            return true;
        }

        public bool SellTower()
        {
            if (selectedTower == null)
                return false;

            int sellValue = selectedTower.SellValue;
            var slot = selectedTower.GetComponentInParent<TowerSlot>();

            if (slot != null)
            {
                slot.RemoveTower();
            }

            Destroy(selectedTower.gameObject);
            GameManager.Instance.AddCurrency(sellValue);
            ClearSelection();

            return true;
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 19: HUD Controller

**Files:**
- Create: `Assets/Scripts/UI/HUDController.cs`

**Step 1: Create HUDController with Canvas UI**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Core;

namespace TowerDefense.UI
{
    public class HUDController : MonoBehaviour
    {
        private Text livesText;
        private Text waveText;
        private Text currencyText;
        private Button startWaveButton;
        private GameObject towerPanel;
        private GameObject towerInfoPanel;

        private Canvas canvas;
        private WaveManager waveManager;
        private TowerManager towerManager;

        private void Awake()
        {
            CreateUI();
        }

        private void Start()
        {
            waveManager = FindObjectOfType<WaveManager>();
            towerManager = FindObjectOfType<TowerManager>();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLivesChanged += UpdateLives;
                GameManager.Instance.OnCurrencyChanged += UpdateCurrency;
                GameManager.Instance.OnWaveChanged += UpdateWave;
                GameManager.Instance.OnGameOver += ShowGameOver;

                UpdateLives(GameManager.Instance.Lives);
                UpdateCurrency(GameManager.Instance.Currency);
                UpdateWave(GameManager.Instance.Wave);
            }

            if (towerManager != null)
            {
                towerManager.OnSlotSelected += ShowTowerPanel;
                towerManager.OnTowerSelected += ShowTowerInfo;
                towerManager.OnSelectionCleared += HidePanels;
            }
        }

        private void CreateUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("HUD_Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Top bar background
            CreatePanel(canvasObj.transform, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -30), new Vector2(400, 60), new Color(0, 0, 0, 0.7f));

            // Lives text
            livesText = CreateText(canvasObj.transform, "LivesText", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(80, -30), "Lives: 10");

            // Wave text
            waveText = CreateText(canvasObj.transform, "WaveText", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -30), "Wave: 0");

            // Currency text
            currencyText = CreateText(canvasObj.transform, "CurrencyText", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-80, -30), "Gold: 200");

            // Start Wave button
            startWaveButton = CreateButton(canvasObj.transform, "StartWaveButton", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 100), new Vector2(160, 50), "Start Wave", OnStartWaveClicked);

            // Tower selection panel (hidden by default)
            towerPanel = CreateTowerPanel(canvasObj.transform);
            towerPanel.SetActive(false);

            // Tower info panel (hidden by default)
            towerInfoPanel = CreateTowerInfoPanel(canvasObj.transform);
            towerInfoPanel.SetActive(false);
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = panel.AddComponent<Image>();
            image.color = color;

            return panel;
        }

        private Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, string content)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent);

            var rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(150, 40);

            var text = textObj.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            return text;
        }

        private Button CreateButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent);

            var rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 0.2f);

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            // Button text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            return button;
        }

        private GameObject CreateTowerPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "TowerPanel", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 200), new Vector2(350, 80), new Color(0, 0, 0, 0.8f));

            // Will be populated with tower buttons dynamically
            return panel;
        }

        private GameObject CreateTowerInfoPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "TowerInfoPanel", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 200), new Vector2(200, 100), new Color(0, 0, 0, 0.8f));

            CreateButton(panel.transform, "SellButton", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 30), new Vector2(120, 40), "Sell", OnSellClicked);

            return panel;
        }

        private void UpdateLives(int lives)
        {
            if (livesText != null)
                livesText.text = $"Lives: {lives}";
        }

        private void UpdateCurrency(int currency)
        {
            if (currencyText != null)
                currencyText.text = $"Gold: {currency}";
        }

        private void UpdateWave(int wave)
        {
            if (waveText != null)
                waveText.text = $"Wave: {wave}";
        }

        private void ShowTowerPanel(TowerSlot slot)
        {
            towerPanel.SetActive(true);
            towerInfoPanel.SetActive(false);
            // TODO: Populate with tower buttons based on TowerManager.AvailableTowers
        }

        private void ShowTowerInfo(Entities.Tower tower)
        {
            towerPanel.SetActive(false);
            towerInfoPanel.SetActive(true);
        }

        private void HidePanels()
        {
            towerPanel.SetActive(false);
            towerInfoPanel.SetActive(false);
        }

        private void ShowGameOver()
        {
            // Simple game over display
            CreateText(canvas.transform, "GameOverText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, "GAME OVER");
        }

        private void OnStartWaveClicked()
        {
            waveManager?.StartWave();
        }

        private void OnSellClicked()
        {
            towerManager?.SellTower();
        }
    }
}
```

**Step 2: Verify compilation**

Check Unity console for any compilation errors.

---

## Task 20: Scene Setup & Integration

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (via MCP)

**Step 1: Clear existing scene objects (keep Camera and Light)**

**Step 2: Configure Main Camera**
- Position: (0, 50, 0)
- Rotation: (90, 0, 0)
- Projection: Orthographic
- Size: 25
- Add CameraController component

**Step 3: Create GameController object**
- Add GameManager component
- Add WaveManager component
- Add TowerManager component

**Step 4: Create UI object**
- Add HUDController component

**Step 5: Create materials**
- HexBase (light gray)
- HexPath (dark gray)
- Castle (gold)

**Step 6: Create TowerData assets**
- Arrow Tower (cost: 50, damage: 5, range: 3, fireRate: 1)
- Cannon Tower (cost: 100, damage: 15, range: 2.5, fireRate: 0.5, areaRadius: 1)
- Slow Tower (cost: 75, range: 3.5, slowMultiplier: 0.5, slowDuration: 2)

**Step 7: Assign references in GameManager**
- Assign materials to GameManager

**Step 8: Test the game**
- Enter Play mode
- Verify 7 hex pieces generate
- Verify castle is at center
- Verify tower slots appear
- Click "Start Wave" to spawn enemies
- Verify enemies follow paths to castle

---

## Summary

This implementation plan covers:
1. **Grid System** (Tasks 1-6): Hex coordinates, utilities, piece types, mesh generation
2. **Map Generation** (Task 5): Algorithm to create 7 connected pieces
3. **Pathfinding** (Task 12): BFS-based path calculation for enemies
4. **Entities** (Tasks 13-15): Enemies, towers, projectiles
5. **Game Logic** (Tasks 10, 16, 18): GameManager, WaveManager, TowerManager
6. **UI** (Tasks 17, 19): Camera controls, HUD
7. **Integration** (Task 20): Scene setup and testing

Total: ~20 tasks, each completable in 5-15 minutes.
