# Tower Defender

## Core Idea

A tower defense game where you build the map with hex pieces that are given to you. Expand outward from a central castle, placing path tiles to create routes that enemies walk along, then defend those paths with towers.

## Inspirations

- Rogue Tower
- Loop Hero
- BallPit
- Factorio

## Scenes

### Main Menu

A screen with different areas to enter:
- **Continuous**: Start a run in continuous mode (endless, scaling difficulty) — the primary game mode
- **Battlefield**: Start a run in wave mode
- **Lab**: Spend banked resources on permanent upgrades

Resource totals are displayed at the top.

### Lab

A list of permanent upgrades purchasable with banked resources. Progress is saved across runs via PlayerPrefs.

### Game (Run)

The gameplay scene shared by both wave and continuous modes.

## Game Modes

### Continuous Mode (Primary)

Enemies spawn endlessly from random spawn points with scaling difficulty. There are no discrete waves or build phases. The player can open the upgrade shop at any time via the Upgrades button without pausing the game. Mines collect resources at a constant rate.

The player can escape the run after it finishes the objective in the run (or cheat shortcut), banking all gathered resources. The player can also decide to stay in the run to gather more resources

Continuous spawn details:
- Spawn interval: starts at 2s, decays to 0.3s minimum over ~4 minutes
- Health scaling: +1x per minute elapsed
- Speed scaling: +1x per 5 minutes elapsed
- Flying enemies: 25% chance after 30 seconds
- Cart enemies: 15% chance after 5 minutes

### Wave Mode (Alternative)

Traditional tower defense with discrete waves. Between waves the player gets a build phase (30s) and an upgrade shop. Defeating all enemies in a wave awards a gold bonus of 50 + (wave - 1) * 10.

## Currencies

### Run-Specific
- **Gold**: Earned by killing enemies. Lost when the run ends. Used to place pieces, build towers, build mods.

### Persistent Resources
- **Iron Ore** (silver/gray)
- **Gems** (purple)
- **Florpus** (green)
- **Adamantite** (red)

Persistent resources are gathered during runs from mining outposts. If the player voluntarily exits a run, gathered resources are banked. If the player dies, all run resources are lost. Boss kills award +3 of each resource type.

## Gameplay Loop

### Starting a Run

The player starts with a castle at the center and 2 auto-generated path tiles. Ore patches are generated based on zone distance.

Starting stats (before lab bonuses):
- Lives: 10
- Gold: 200

### Piece Placement

The player has a hand of piece cards. Pieces are dragged onto valid hex positions adjacent to existing tiles. Placing a piece on an occupied tile replaces it (refunding any towers). Pieces cannot be placed if doing so would disconnect existing paths.

### Continuous Flow

1. Player places pieces and towers freely
2. Enemies spawn continuously from random spawn points
3. Mines collect resources every 30 seconds (with visual countdown timer)
4. Player opens the upgrade shop at any time via the Upgrades button
5. After completing the objective, escape becomes available to bank resources and exit

### Wave Flow (Wave Mode)

1. Player places pieces and towers during build phase
2. Player starts a wave (or the 30s build timer expires)
3. Enemies spawn at open edges of the map and walk toward the castle
4. Enemies that reach the castle cost the player a life
5. After all enemies die, a 3-second pause occurs, then the upgrade shop opens
6. Player buys upgrades with gathered resources, then starts the next build phase

### Losing

When lives reach 0, the game ends. All run resources are lost. The player returns to the main menu.

### Exiting Voluntarily

The player can exit between waves (wave mode) via the Exit Run button. All gathered resources are banked permanently. In continuous mode, the escape mechanic becomes available after 5 minutes.

## Hex Grid

Uses flat-top axial coordinates (q, r). Outer radius 20, inner radius ~17.32.

Pieces connect via edges (0-5). Edge directions are at 60 * edge + 30 degrees. Neighbors are found via HexCoord.Directions[edge]. Opposite edge = (edge + 3) % 6.

## Pieces

| Piece | Edges | Cost | Cooldown | Tower Slots | Notes |
|-------|-------|------|----------|-------------|-------|
| Castle | 1 | - | - | No | Center of the map, enemy target |
| Bend | 2 (adjacent) | 50g | 15s | Yes | |
| Fork | 3 | 75g | 20s | Yes | T-junction |
| Cross | 4 | 100g | 25s | Yes | |
| Star | 5 | 150g | 35s | Yes | |
| Crossroads | 6 | 200g | 45s | Yes | All edges connected |
| Goblin Camp | 2 | 50g | 15s | No | Enemy spawner (burst spawn) |


## Spawn Points

Any piece with an open edge (connected edge leading to empty hex) becomes a wave spawn point. Enemies spawn at the edge midpoint of the open edge, not the hex center. Dead End pieces are always spawn points.

Pulsing goblin heads indicators mark spawn points. They are visible during the build phase and between waves, and hidden when a wave starts. In continuous mode they stay visible at all times.

## Towers

Towers are placed on tower slots attached to path pieces (max 2 slots per piece). Towers can be sold for 50% of their cost.

| Tower | Cost | Damage | Range | Fire Rate | Special |
|-------|------|--------|-------|-----------|---------|
| Arrow | 100g | 5 | 8 | 1.0s | Targets flying, prioritizes flying |
| Cannon | 100g | 15 | 12 | 1.5s | AoE damage (radius 3) |
| Flame | 90g | - | 8 | 0.5s | Spawns fire patches (5 DPS, 4s duration, 2s burn) |
| Shotgun | 75g | 8 | 8 | 1.2s | 5 projectiles, 60-degree spread |
| Slow | 75g | 0 | 5 | 1.0s | Slows enemies to 0.5x speed for 2s |
| Tesla | 120g | 8 | 10 | 0.8s | Chain lightning (3 bounces, 0.7x falloff), targets flying, prioritizes flying |

Flame, Shotgun, Slow, and Tesla towers must be unlocked in the Lab before they appear in the build menu.

Only Arrow and Tesla towers can target flying enemies.

## Enemies

All enemy stats are defined in EnemyData ScriptableObjects. Each type has a complete prefab with visuals, collider, and Enemy component.

### Ground Enemies
- Base health: 10, scaling: +5 per wave
- Base speed: 2.0, scaling: +0.1 per wave
- Gold reward: 10 + (wave - 1) * 2
- Visual: red capsule

### Flying Enemies
- Appear from wave 3 onward (wave mode) or after 30s (continuous)
- Count per spawn point: 1 + (wave - 3) / 2
- Base speed: 2.6, base health: 10, reward: 15
- Fly at Y=4, visually distinct (orange, flattened sphere with wings)
- Only targetable by Arrow and Tesla towers

### Cart Enemies
- Appear from wave 3 onward (wave mode) or after 5 min (continuous, 15% chance)
- Count per spawn point: 1 + (wave - 3) / 3
- Base health: 20 (2x ground), base speed: 1.2 (0.6x ground), reward: 12
- 1.5x visual scale, brown wagon with roof and wheels
- On death: spawns 3 ground goblins with 0.5x health, 1.1x speed

### Boss Enemies
- Spawned when the player places a piece entering a new zone
- 10x zone health multiplier, 0.6x speed
- 2x visual scale, dark purple color
- 200g reward + 3 of each resource type
- One boss per new zone, spawns on the next wave after zone entry

## Zone System

Concentric difficulty zones based on hex distance from the castle. Zone boundaries default to distances {3, 6, 9}.

| Zone | Distance | Health Multiplier | Speed Multiplier |
|------|----------|-------------------|------------------|
| 1 | 0-3 | 1.0x | 1.0x |
| 2 | 4-6 | 1.5x | 1.1x |
| 3 | 7-9 | 2.0x | 1.2x |
| 4 | 10+ | 2.5x | 1.3x |

Visual zone rings (colored circles on the ground) mark zone boundaries: yellow, orange, red, dark red.

## Mining and Resources

### Ore Patches
Generated at map start. Zone 1 gets Iron Ore and Gems (6 nodes). Zone 2+ gets Florpus and Adamantite (4 nodes). Patches are marked with floating resource sprites and hex outline rings.

### Mining Outposts
- Built on ore patches via the tower panel
- In wave mode: collect resources after each wave
- In continuous mode: collect resources at a constant rate, with a visible clock indicator (progress ring + countdown) above each mine
- Resource popup animation flies from mine to castle on collection
- Tiles with mines cannot be replaced

### Lures
- Cost: 75g
- Placed on any non-castle tile as a modifier
- Next wave spawns 3 + wave number enemies at the lure position
- Lured enemies give 2x gold
- One-time use: consumed after the wave spawns
- Tiles with lures cannot be replaced

## In-Run Upgrades

Purchased during runs with gathered resources. Available in the upgrade shop (between waves in wave mode, any time in continuous mode).

| Upgrade | Effect | Cost Resource | Base Cost | Per Level | Max Level |
|---------|--------|---------------|-----------|-----------|-----------|
| Swift Towers | +5% tower attack speed | Iron Ore | 5 | +3 | 10 |
| Heavy Rounds | +5% tower damage | Gems | 5 | +3 | 10 |
| Risky Investment | +10% enemy speed, +20% gold | Florpus | 3 | +2 | 10 |
| Barrage | +1 extra projectile | Adamantite | 8 | +5 | 5 |

## Lab Upgrades (Permanent)

Purchased between runs with banked resources. Progress saved via PlayerPrefs.

### Stat Upgrades

| Upgrade | Effect | Cost Resource | Base Cost | Per Level | Max Level |
|---------|--------|---------------|-----------|-----------|-----------|
| Gold Reserve | +50 starting gold | Iron Ore | 5 | +5 | 5 |
| Fortification | +1 starting life | Gems | 10 | +10 | 3 |
| Heavy Caliber | +10% tower damage | Florpus | 8 | +6 | 5 |
| Rapid Fire | +10% tower speed | Iron Ore | 8 | +6 | 5 |
| Quick Deploy | -10% piece cooldown | Adamantite | 12 | +8 | 3 |

### Tower Unlocks

| Tower | Cost Resource | Cost |
|-------|---------------|------|
| Slow | Iron Ore | 10 |
| Flame | Florpus | 15 |
| Shotgun | Gems | 20 |
| Tesla | Adamantite | 25 |

## UI

### HUD Elements
- Lives bar (top, shows current/max lives)
- Gold display
- Resource panel (top-right): banked + run-gathered counts for each resource
- Start Wave / Start Now button (wave mode only)
- Exit Run / Escape button
- Build phase countdown timer
- Upgrades button (always visible, both modes, purple)
- Tower build/sell panel (appears when selecting tower slots)
- Piece hand (bottom of screen, draggable cards with tabs for Paths/Towers/Mods)

### Upgrade Shop Overlay
- Full-screen dark overlay with card rows
- Each card shows name, level, resource cost, and buy button
- Wave mode: pauses game, shows Next Wave and Exit Run buttons
- Opened via Upgrades button: does not pause, shows Close button instead
