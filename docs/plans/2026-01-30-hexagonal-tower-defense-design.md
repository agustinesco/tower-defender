# Hexagonal Tower Defense - Game Design

## Overview

A top-down mobile tower defense game with a hexagonal map system. The map consists of 7 interconnected hex pieces with a castle at the center. Enemies spawn from edge pieces and travel along paths toward the castle. Players place towers on slots alongside paths to defend.

## Core Specifications

- **Genre:** Tower Defense
- **Platform:** Mobile (touch controls)
- **Camera:** Fixed top-down, orthographic
- **Visual style:** Minimalist/geometric

---

## 1. Hexagonal Grid System

The map uses a **flat-top hexagonal grid** with axial coordinates (q, r). Each hex piece is a self-contained unit with:

- **6 edges** numbered 0-5 (starting from right, going counter-clockwise)
- **Path connections** on 1-3 edges that can link to neighboring hexes
- **Visual representation** using Unity's mesh generation for clean geometric hexagons

**Hex dimensions:**
- Outer radius: 5 units (center to vertex)
- Inner radius: ~4.33 units (center to edge midpoint)

**Coordinate system:**
- Castle piece at origin (0, 0)
- Neighbors calculated using axial coordinate offsets

---

## 2. Hex Piece Types & Path Layout

**Piece types based on connection count:**

| Type | Connections | Description |
|------|-------------|-------------|
| **Castle** | 1 | Center piece with castle structure, single exit path |
| **Straight** | 2 | Path enters one edge, exits opposite edge |
| **Bend** | 2 | Path enters one edge, exits adjacent edge (60° or 120° turn) |
| **Fork** | 3 | Path splits into two directions (T-junction) |
| **Dead-end** | 1 | Path enters, terminates (enemy spawn point) |

**Path rendering:**
- Paths are 1.5 units wide, rendered as flat quads following bezier curves between edge midpoints
- Paths curve smoothly through the hex center area
- Distinct ground color (dark gray) against hex base (light gray)

**Tower slots:**
- Generated automatically at fixed distances along each path segment
- Positioned 1.2 units perpendicular to the path
- Each path segment gets 2 slots (one per side)
- A hex with 2 paths = 4 slots, 3 paths = 6 slots

---

## 3. Map Generation Algorithm

**Initial generation (7 pieces):**

1. Place **Castle piece** at center (0,0) with path connection on a random edge
2. From the castle's connection, grow a **linear path** of 6 pieces using these rules:
   - Each new piece connects to the previous piece's open edge
   - Randomly select piece type (Straight, Bend) weighted 60/40
   - Final piece is always a **Dead-end** (spawn point)
3. If a Fork piece is placed, one branch continues the main path, the other gets a Dead-end

**Constraints:**
- No overlapping hexes (check coordinates before placing)
- Path must form a connected graph back to castle
- Minimum 2 dead-ends guaranteed (spawn points)

**Example generation flow:**
```
Castle → Bend → Straight → Fork → Dead-end
                              ↓
                          Straight → Dead-end
```

---

## 4. Enemy System & Pathfinding

**Enemy behavior:**
- Spawn at dead-end hexes (edge pieces)
- Follow paths toward castle using **waypoint navigation**
- Each path segment has pre-calculated waypoints along its bezier curve
- Enemies move from waypoint to waypoint at constant speed

**Pathfinding approach:**
- At game start, calculate shortest path from each dead-end to castle using BFS on the hex graph
- Store path as list of hex coordinates + entry/exit edges
- Enemies follow pre-computed paths (no runtime pathfinding needed)

**Wave system:**
- Waves spawn every 20 seconds
- Each wave spawns X enemies from each dead-end (staggered 0.5s apart)
- Wave difficulty increases: more enemies, faster speed
- If enemy reaches castle, player loses 1 life

**Enemy stats:**
- Health: starts at 10, scales with wave number
- Speed: 2 units/second base

---

## 5. Towers & Combat

**Tower placement:**
- Tap an empty slot to open tower selection UI
- Select tower type, deduct currency, instantiate tower
- Towers can be sold for 50% refund

**Tower types:**

| Tower | Cost | Damage | Range | Fire Rate | Behavior |
|-------|------|--------|-------|-----------|----------|
| **Arrow** | 50 | 5 | 3 units | 1/sec | Single target, first enemy in range |
| **Cannon** | 100 | 15 | 2.5 units | 0.5/sec | Area damage (1 unit radius) |
| **Slow** | 75 | 0 | 3.5 units | 1/sec | Slows enemies by 50% for 2 seconds |

**Combat flow:**
- Towers scan for enemies within range each frame
- When target found, rotate toward enemy (visual only)
- Fire projectile at fire rate interval
- Projectile travels to target, applies damage/effect on hit
- Enemy dies when health ≤ 0, awards currency

**Currency:**
- Start with 200
- Earn 10 per enemy kill
- Earn 50 bonus for completing a wave

---

## 6. UI & Mobile Controls

**Camera setup:**
- Orthographic camera, fixed top-down (no rotation)
- Initial zoom shows entire 7-hex map with padding
- Pinch to zoom: 0.5x to 2x range
- Drag to pan within map bounds

**HUD elements:**
- **Top bar:** Lives (left), Wave counter (center), Currency (right)
- **Bottom:** Tower selection panel (appears when slot tapped)
- **Wave start button:** Centered, visible between waves

**Touch interactions:**
- **Tap hex:** Select hex, highlight its slots
- **Tap empty slot:** Open tower build menu
- **Tap tower:** Show tower info, sell button
- **Drag anywhere:** Pan camera
- **Pinch:** Zoom camera

**Visual feedback:**
- Selected slot: pulsing outline
- Affordable towers: full color
- Unaffordable towers: grayed out
- Range preview: circle shown when placing/selecting tower

---

## 7. Unity Architecture

**Folder structure:**
```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs      # Game state, waves, win/lose
│   │   └── CurrencyManager.cs  # Currency tracking
│   ├── Grid/
│   │   ├── HexGrid.cs          # Coordinate math, neighbor lookup
│   │   ├── HexPiece.cs         # Single hex data + mesh generation
│   │   └── MapGenerator.cs     # 7-piece map generation
│   ├── Entities/
│   │   ├── Enemy.cs            # Movement, health, death
│   │   ├── Tower.cs            # Targeting, firing
│   │   ├── Projectile.cs       # Movement, hit detection
│   │   └── TowerSlot.cs        # Placement point
│   └── UI/
│       ├── HUDController.cs    # Lives, currency, wave display
│       ├── TowerPanel.cs       # Build menu
│       └── CameraController.cs # Pan, zoom
├── Prefabs/
├── Materials/
└── Scenes/
    └── GameScene.unity
```

No external dependencies - all using built-in Unity features.
