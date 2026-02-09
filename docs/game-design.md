# Tower Defender

## Core idea

A tower defense game where you build the map with pieces that are given to you

## Inspirations

- Rogue Tower
- Loop Hero
- BallPit
- Factorio

## Menus

### Main Screen

A map with different places to click and enter another scene:
- **Embark**: Button to start a run
- **Lab**: Spend currencies to permanently upgrade

### Lab

A list of purchasable upgrades that passively increase the player's power.

### Embark

The game scene where the fun happens.

## Currencies

### Run-specific
- **Gold coins**: Lost when you exit the run

### Persistent currencies
- **Iron ore**
- **Gems**
- **Florpus**
- **Adamantite**

## Gameplay Loop

### Inside Run

On each run you start with a generated first layout that spawns enemies at the end of the path in certain special tiles that spawn enemies.

Defeating enemies awards gold coins that are only used to build paths and place towers.

The objective in the run is to:

1. **Collect resources**: To do so the player needs to save enough money to place a mining tile in one of the randomly generated patches of resources. Resources may be used for:
   - Improve turret power in between waves
   - If the player decides to leave the game after a wave he will maintain the resources that can be used for permanent upgrades
   - The further away the resources are from the origin, the more resources they produce

2. **Reach a boss**: The map will be divided in circles of difficulty. To be able to proceed to the next area the player needs to first defeat a boss that will be spawned on the next wave after the player reaches the edge of a circle.

### Outside Run

Player will have a base:
- City
- Base of operations
- Lab

With the resources gathered in a run they should be able to buy/research permanent upgrades/turrets that make runs faster and allow the player to go further into the mine.

### Run Specifics

The player will have categories of pieces to place:

- **Path**: The simplest one, only a path that the player can change to have bifurcations. Defenses can be placed alongside paths that don't have any modifiers.
- **Mining outpost**: This puts a modifier on a path piece to be able to gather resources when placed on an ore patch. Enemies can randomly decide to go to these pieces instead of the main base so they need to be defended too, but not as heavily as the main base.
- **Enemy lure**: This puts a modifier on a path piece to turn it into an enemy spawner on the next wave.
- **Enemy spawners**: While building the map a path can encounter an enemy spawner piece. When connected to the main path these pieces will spawn enemies on each wave that will mainly go towards the main base.

### Towers

To defend the paths from enemies the player will have towers that can be placed on tiles without modifications:

- **Arrow tower**: Shoots a single projectile at an enemy.
- **Cannon tower**: Shoots a single projectile that deals AoE damage.
- **Slow tower**: Generates a field around it that slows down enemies while in contact with the field or for a brief moment after leaving it.
- **Shotgun tower**: Shoots many projectiles that pass through enemies without aiming, towards the nearest path when detecting nearby enemies.
- **Tesla tower**: Shoots an arcing ray that bounces on enemies.
- **Flame tower**: Shoots fire to the nearest path when detecting enemies nearby, spawning a small patch of fire on the path that lights up enemies on fire dealing damage over time.
