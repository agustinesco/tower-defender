# Projectile System Design

## Overview

Replace instant tower damage with visual projectiles that travel toward enemies with a trail effect.

## Projectile Component

**New Projectile.cs script** - MonoBehaviour handling projectile movement and effects.

**Properties:**
- `speed` - Travel speed (from TowerData.projectileSpeed, default 15)
- `target` - Enemy being tracked (can become null if enemy dies)
- `targetLastPosition` - Stores position if target dies mid-flight
- `damage` - Damage to deal on hit
- `isAreaDamage` / `areaRadius` - For area damage towers
- `appliesSlow` / `slowMultiplier` / `slowDuration` - For slow towers
- `towerColor` - Color for sphere and trail

**Visual setup (created on spawn):**
- Small sphere (scale 0.3) with tower's color
- TrailRenderer with matching color, fading alpha
- Trail time 0.25 seconds

**Behavior:**
- Move toward target each frame (or targetLastPosition if target dead)
- On reaching destination: apply damage/slow if target exists, destroy self
- Auto-destroy after 3 seconds (safety cleanup)

## Tower Integration

**Tower.cs changes:**
- `Fire()` spawns projectile(s) instead of instant damage
- Projectile created at `turretHead.position`
- Passes damage, target, color, special properties
- Extra projectiles (Barrage upgrade): spawn toward different enemies with spread
- Remove Debug.DrawLine calls (trail replaces visual feedback)

**Tower types:**
- Single target: projectile hits one enemy
- Area damage: projectile travels to target, applies damage to all in radius on impact
- Slow tower: projectile applies slow on hit

## TowerData Changes

**New field:**
- `projectileSpeed` (float, default 15f)

## Visual Details

**Trail settings:**
- Start width: 0.15
- End width: 0
- Time: 0.25 seconds
- Color: tower color fading to transparent
- Material: Sprites-Default (no custom material needed)

## Cleanup

- Projectiles auto-destroy on hit or timeout
- No object pooling (simple scope)
