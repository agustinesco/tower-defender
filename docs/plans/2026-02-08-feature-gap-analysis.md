# Feature Gap Analysis: Design vs Implementation

## What's Implemented

- Hexagonal grid with flat-top axial coordinates and pathfinding (BFS)
- Player-driven tile placement with validation, ghost previews, and rotation cycling
- Piece types: Castle, Straight, Bend, Fork, DeadEnd, GoblinCamp
- Piece cards with cooldowns and gold cost
- 4 tower types: Arrow (single target), Cannon (AoE), Slow (aura), Shotgun (piercing spread)
- Tower placement on dynamic slots alongside paths, with sell for refund
- Enemy spawning via waves (WaveManager) and goblin camps (GoblinCampSpawner)
- Projectile system with homing and directional/piercing modes
- Wave-based progression with configurable WaveData
- Roguelike upgrade cards after each wave (4 types: TowerSpeed, TowerDamage, EnemySpeedAndGold, ExtraProjectiles)
- Full programmatic UI: HUD, piece hand, tower selection, upgrade selection, picked cards viewer
- Orthographic camera with touch pan/zoom
- Single currency: gold (used for everything)
- Single scene, no menus

---

## Missing Features

### 1. Menu System and Scene Flow (not implemented)
The design specifies a main screen as a map with clickable locations leading to different scenes:
- **Embark** button to start a run
- **Lab** button to open permanent upgrade screen

Currently there is only one scene (the run itself) with no main menu, no lab screen, and no way to start/end runs.

### 2. Currency System (partially implemented)
**Current:** Single gold currency used for towers and piece placement, resets on game over.

**Design specifies two categories:**
- **Run-specific**: Gold coins (lost on exit) — this exists but is also used for towers and paths, which matches the updated design
- **Persistent currencies** (4 types): Iron ore, Gems, Florpus, Adamantite — none of these exist

The persistent currencies survive between runs and are spent in the Lab for permanent upgrades. They are gathered via mining outposts during runs.

### 3. Resource / Mining System (not implemented)
The player needs to save gold to place a mining outpost on randomly generated ore patches. Each persistent currency type (Iron ore, Gems, Florpus, Adamantite) would correspond to different ore patches on the map.

**What's needed:**
- Ore patch generation on the map (visual indicators at certain coordinates)
- Different ore types mapping to the 4 persistent currencies
- Mining outpost piece type (path modifier)
- Resource production per wave, scaling with distance from castle
- Resource collection UI showing gathered amounts
- Resources kept when player voluntarily exits after a wave

### 4. Mining Outpost Piece (not implemented)
A path modifier piece that gathers resources when placed on an ore patch. Enemies can randomly target mining outposts instead of the castle, so they need defense.

### 5. Enemy Lure Piece (not implemented)
A path modifier that turns a tile into an enemy spawner on the next wave. Player-placed to attract enemies for strategic reasons (funneling, bonus rewards).

### 6. Enemy Routing to Mining Outposts (not implemented)
Enemies can randomly decide to go to mining outpost pieces instead of the main base. This requires multi-destination pathfinding and some consequence when enemies reach a mining outpost (resource loss, outpost damage/destruction).

### 7. Pre-placed Enemy Spawners (different from current)
**Design:** Spawner tiles already exist on the unexplored map. When a player builds a path adjacent to one, it gets revealed and connected, spawning enemies on future waves.

**Current:** GoblinCamp is a player-placed piece from the hand.

### 8. Difficulty Circles and Boss System (not implemented)
The map is divided into concentric circles of difficulty. When the player reaches the edge of a circle by placing pieces, a boss spawns on the next wave. Defeating the boss unlocks the next area.

### 9. Tesla Tower (not implemented)
Shoots an arcing ray that bounces between enemies (chain lightning).

### 10. Flame Tower (not implemented)
Shoots fire toward the nearest path when detecting enemies, spawning a fire patch that deals damage over time.

### 11. Lab / Permanent Upgrades (not implemented)
A dedicated screen where players spend persistent currencies (Iron ore, Gems, Florpus, Adamantite) on permanent upgrades that passively increase player power across all future runs.

### 12. Save/Load and Persistence (not implemented)
No persistence system exists. Persistent currencies, unlocked upgrades, and unlocked towers all need to survive between runs and app sessions.

### 13. Voluntary Run Exit (not implemented)
The design says the player can choose to leave a run after a wave to bank gathered resources. Currently the only way a run ends is death (game over).

---

## Differences From Design

| Aspect | Design | Current Implementation |
|--------|--------|----------------------|
| Scenes | Main menu map + Lab + Run scene | Single run scene only |
| Gold usage | Build paths and place towers only | Also used for everything (no other currencies) |
| Persistent currencies | 4 types (Iron ore, Gems, Florpus, Adamantite) | None |
| Starting layout | Generated first layout with paths | Castle-only, player builds everything |
| Enemy spawners | Pre-placed on map, discovered by building | Player places GoblinCamp from hand |
| Turret improvement | Spend resources between waves | Roguelike upgrade cards (random) |
| Meta progression | Lab with permanent upgrades | None, upgrades reset on game over |
| Map structure | Concentric difficulty circles with bosses | Flat difficulty, no zones |
| Tower roster | 6 types (Arrow, Cannon, Slow, Shotgun, Tesla, Flame) | 4 types (Arrow, Cannon, Slow, Shotgun) |
| Run exit | Voluntary exit after wave to bank resources | Only death ends the run |
| Mining | Ore patches + mining outpost piece | No mining system |
| Enemy routing | Can target castle or mining outposts | Castle only |

---

## Implementation Plan

### Phase 1: Tesla and Flame Towers
The simplest additions since the tower system is already mature. No new systems needed, just new tower behaviors.

**Step 1.1: Tesla Tower**
- Add chain bounce fields to TowerData: `bounceCount`, `bounceRange`, `damageFalloff`
- Implement chain targeting in Tower.cs: find initial target, then bounce to nearest un-hit enemy within bounce range
- Create lightning visual using LineRenderer connecting bounce targets (instant damage, no projectile travel)
- Create TowerData ScriptableObject asset for Tesla
- Add to HUDController tower button list

**Step 1.2: Flame Tower**
- Add fire-related fields to TowerData: `firePatchDuration`, `fireDamagePerSecond`, `burnDuration`
- Create FirePatch MonoBehaviour: spawns on path position, persists for duration, damages enemies in radius each frame
- Add burn status effect to Enemy.cs (damage over time that continues after leaving the fire patch)
- Implement flame targeting in Tower.cs: detect enemies near path, spawn FirePatch at that path location
- Visual: orange/red ground quad or particle system for fire patches
- Create TowerData ScriptableObject asset for Flame
- Add to HUDController tower button list

### Phase 2: Pre-placed Enemy Spawners
Change spawner tiles from player-placed to map-discovered.

**Step 2.1: Spawner pre-generation**
- Seed hidden spawner locations at distances from castle (e.g., ring 3+) during map initialization
- Store hidden spawners as a set of HexCoord positions in MapGenerator or GameManager
- When player places a piece adjacent to a hidden spawner coord, reveal it: auto-create the spawner piece and connect it to the path network

**Step 2.2: Visual reveal and connection**
- Show a visual hint near hidden spawners (fog, glow, or warning icon on adjacent ghost pieces)
- On reveal: create HexPieceData for spawner, wire into path network, start GoblinCampSpawner
- Remove GoblinCamp from PieceProvider's available pieces (set generation weight to 0)

### Phase 3: Persistent Currencies and Mining System
Introduces the resource economy that drives the meta progression loop.

**Step 3.1: Persistent currency data model**
- Define 4 resource types: IronOre, Gems, Florpus, Adamantite
- Add persistent currency storage to a new PersistenceManager or extend GameManager
- UI display in HUD showing each resource count during runs

**Step 3.2: Ore patch generation**
- Generate random ore patch locations on the map (clusters of coords at various distances from castle)
- Each ore patch has a resource type (one of the 4 persistent currencies)
- Visual indicator on empty hexes showing ore (colored ground marker visible before piece placement)
- Yield scales with distance from castle

**Step 3.3: Mining outpost piece**
- New HexPieceType: MiningOutpost (functions as a path piece with a mining modifier)
- New HexPieceConfig with higher gold cost
- When placed on an ore patch coord, activates resource production
- Resources accumulate per wave: `baseYield * distanceMultiplier` per active outpost
- If placed on a non-ore coord, it's just an expensive path piece

**Step 3.4: Mining outpost defense**
- Mining outposts are valid enemy targets (random chance per enemy to path toward an outpost)
- Consequence when enemies reach an outpost: resource loss or temporary shutdown

### Phase 4: Enemy Routing and Lure Piece

**Step 4.1: Multi-destination pathfinding**
- PathFinder supports multiple destination types (castle + mining outposts)
- Each enemy randomly selects a destination on spawn (weighted toward castle)
- Enemies that reach a mining outpost cause resource loss or outpost damage

**Step 4.2: Enemy lure piece**
- New piece type that acts as a path modifier
- On the next wave after placement, becomes an additional enemy spawn point
- Spawned enemies give bonus gold rewards
- Lure gives the player a controlled way to increase difficulty for more rewards

### Phase 5: Difficulty Circles and Bosses

**Step 5.1: Zone system**
- Define concentric distance rings from castle (e.g., ring 1: distance 1-3, ring 2: 4-6, etc.)
- Track which zones the player has placed pieces in
- Enemy difficulty (health/speed multipliers) scales with zone of their spawn point
- Visual: subtle ground color or ring border indicating zone boundaries

**Step 5.2: Boss system**
- When player first places a piece crossing into a new zone, flag next wave as a boss wave
- Boss enemy: larger model, high health, possibly special abilities
- Boss defeat unlocks the zone permanently
- Boss rewards: extra gold + persistent resources

### Phase 6: Save/Load and Voluntary Exit

**Step 6.1: Save/load system**
- Persistent data: resource bank, unlocked upgrades, unlocked towers
- Use JSON file or PlayerPrefs for save data
- Load on app start, save after each run ends

**Step 6.2: Voluntary run exit**
- After a wave completes, show option to "Leave Run" alongside "Start Next Wave"
- Leaving banks all gathered persistent resources
- Death: bank partial resources (e.g., 50%)

### Phase 7: Menu System and Lab

**Step 7.1: Main menu scene**
- Map-style screen with clickable locations
- Embark button: loads the run scene
- Lab button: opens the lab screen
- Display persistent currency totals

**Step 7.2: Lab screen**
- List of purchasable permanent upgrades
- Each upgrade costs persistent currencies
- Categories: tower stats, starting gold, lives, piece cooldowns, new tower unlocks
- Upgrades are passive and permanent — apply automatically on every future run

**Step 7.3: Run flow integration**
- Main Menu → Run Scene (Embark) → Run ends → Main Menu
- On run end: save persistent currencies, show summary of gathered resources
- Lab accessible from main menu at any time
