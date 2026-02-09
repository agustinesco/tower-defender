# Tower Defender

A tower defense game where you build the map with hex pieces. Expand outward from a central castle, create paths for enemies to walk along, and defend them with towers. Inspired by Rogue Tower, Loop Hero, BallPit, and Factorio.

## Features

- **Hex-based map building**: drag and place path pieces from a hand of cards to expand the battlefield
- **Two game modes**: wave-based with build phases and upgrade shops, or continuous with endless scaling difficulty
- **6 tower types**: Arrow, Cannon, Flame, Shotgun, Slow, Tesla — each with unique mechanics
- **Flying and ground enemies**: flying enemies bypass most towers, only Arrow and Tesla can target them
- **Zone system**: concentric difficulty zones with boss encounters when entering new zones
- **Resource mining**: build outposts on ore patches to gather persistent resources
- **Persistent progression**: spend banked resources in the Lab to unlock towers and buy permanent upgrades
- **In-run upgrades**: spend gathered resources between waves to boost tower stats or take risks for more gold

## Tech

- Unity 2022.3
- All UI built programmatically (no prefabs for UI elements)
- C# with namespace `TowerDefense` (sub-namespaces: Core, Grid, UI, Entities, Data)

## Game Design

See [docs/game-design.md](docs/game-design.md) for the full game design document with all mechanics, stats, and formulas.
