# Upgrade System Design

## Overview

A roguelike-style upgrade system where players choose from 3 upgrade cards after each wave. Upgrades apply only to the current run and stack additively.

## Core Data Structure

### UpgradeCard ScriptableObject

```csharp
public enum CardRarity { Common, Rare, Epic }
public enum UpgradeEffectType { TowerSpeed, TowerDamage, EnemySpeedAndGold, ExtraProjectiles }
public enum IconShape { Circle, Diamond, Star, Hexagon }

[CreateAssetMenu(fileName = "NewUpgrade", menuName = "Tower Defense/Upgrade Card")]
public class UpgradeCard : ScriptableObject
{
    public string cardName;
    public string description;
    public CardRarity rarity;
    public UpgradeEffectType effectType;
    public float effectValue;
    public float secondaryValue; // For dual effects like EnemySpeedAndGold
    public IconShape iconShape;
}
```

### Rarity Configuration

| Rarity | Color | Weight |
|--------|-------|--------|
| Common | Gray (#808080) | 60% |
| Rare | Blue (#4169E1) | 30% |
| Epic | Purple (#9932CC) | 10% |

## Upgrade Effects System

### UpgradeManager (Singleton)

Tracks active upgrades for the current run:

- `towerSpeedBonus` (float, default 0) - Percentage bonus to tower fire rate
- `towerDamageBonus` (float, default 0) - Percentage bonus to tower damage
- `enemySpeedBonus` (float, default 0) - Percentage bonus to enemy speed
- `enemyGoldBonus` (float, default 0) - Percentage bonus to gold rewards
- `extraProjectiles` (int, default 0) - Additional projectiles per tower shot
- `pickedCards` (List<UpgradeCard>) - All cards picked this run

### Effect Application

- **Towers**: Read bonuses at attack time. `actualFireRate = fireRate * (1 + towerSpeedBonus)`, `actualDamage = damage * (1 + towerDamageBonus)`
- **Enemies**: Read bonuses at spawn time. Speed and gold reward multiplied by `(1 + bonus)`
- **Projectiles**: Tower spawns `1 + extraProjectiles` projectiles with slight angular spread

### Run Reset

All bonuses reset to 0 and `pickedCards` clears when game restarts after game over.

## UI Components

### Upgrade Selection Screen

Displayed after each wave completes, pauses game completely.

- Dark semi-transparent overlay (black, 70% opacity) covering entire screen
- Title: "Choose an Upgrade" at top
- 3 card panels arranged horizontally in center
- Each card has:
  - Colored border matching rarity
  - Placeholder geometric icon in rarity color
  - Card title
  - Description text
- Clicking a card: applies effect, closes overlay, resumes game, triggers map expansion

### View Picked Cards Button

- Small circular button in bottom-left corner
- Shows deck/card icon
- Only visible when at least 1 card picked

### Picked Cards Overlay

- Same dark overlay style
- Title: "Your Upgrades"
- Grid layout of all picked cards
- Duplicate cards show "×N" badge
- Tap outside cards to close

## The Four Cards

### Swift Towers (Common)
- **Effect**: Tower attack speed +5%
- **Icon**: Circle
- **Implementation**: Adds 0.05 to `towerSpeedBonus`

### Heavy Rounds (Common)
- **Effect**: Tower attack damage +5%
- **Icon**: Diamond
- **Implementation**: Adds 0.05 to `towerDamageBonus`

### Risky Investment (Rare)
- **Effect**: Enemies 10% faster, 20% more gold
- **Icon**: Star
- **Implementation**: Adds 0.10 to `enemySpeedBonus`, adds 0.20 to `enemyGoldBonus`

### Barrage (Epic)
- **Effect**: Towers fire +1 additional projectile
- **Icon**: Hexagon
- **Implementation**: Adds 1 to `extraProjectiles`

## Game Flow Integration

1. Wave completes → `OnWaveComplete` fires
2. Game pauses (Time.timeScale = 0)
3. Upgrade selection UI appears with 3 weighted-random cards
4. Player selects a card
5. Effect applied to UpgradeManager
6. UI closes, game unpauses
7. Map expansion proceeds
8. Normal flow continues

## File Structure

```
Assets/
├── Scripts/
│   ├── Core/
│   │   └── UpgradeManager.cs
│   ├── Data/
│   │   └── UpgradeCard.cs
│   └── UI/
│       ├── UpgradeSelectionUI.cs
│       └── PickedCardsUI.cs
├── ScriptableObjects/
│   └── Upgrades/
│       ├── SwiftTowers.asset
│       ├── HeavyRounds.asset
│       ├── RiskyInvestment.asset
│       └── Barrage.asset
└── Prefabs/
    └── UI/
        ├── UpgradeCardPanel.prefab
        └── UpgradeSelectionCanvas.prefab
```
