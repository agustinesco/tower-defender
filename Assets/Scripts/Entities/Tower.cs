using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Grid;

namespace TowerDefense.Entities
{
    public class Tower : MonoBehaviour
    {
        // Minimum range: turret offset from path center (6.5) + path half-width (4.0)
        // Ensures towers can always hit enemies on the far side of the path
        private const float MinRange = 10.5f;

        private TowerData data;
        private float fireCooldown;
        private Enemy currentTarget;
        private GameObject rangeIndicator;
        private Transform turretHead;
        private Vector3 facingDirection; // For shotgun towers
        private GameObject auraIndicator; // For slow aura towers
        private LineRenderer teslaLineRenderer; // For tesla chain lightning
        private float teslaVisualTimer; // How long to show the chain
        private float auraCheckTimer; // Throttle aura tower checks
        private HexCoord? cachedTileCoord;
        private float hasteMultiplier = 1f;

        // Reusable lists to avoid per-frame allocations
        private readonly List<Enemy> _reusableEnemyList = new List<Enemy>();
        private readonly List<Enemy> _reusableTeslaChain = new List<Enemy>();
        private readonly HashSet<Enemy> _reusableTeslaHit = new HashSet<Enemy>();

        public TowerData Data => data;
        public int SellValue => data != null ? data.cost / 2 : 0;
        private float EffectiveRange => data != null ? Mathf.Max(data.range, MinRange) : MinRange;

        public void Initialize(TowerData towerData)
        {
            data = towerData;
            fireCooldown = 0f;
            CacheTileCoord();
            CreateVisual();

            // For shotgun towers, calculate and set facing direction toward path
            if (data.isShotgun)
            {
                SetupShotgunFacing();
            }

            // For slow aura towers, create the visible aura
            if (data.appliesSlow)
            {
                CreateAuraIndicator();
            }
        }

        private void SetupShotgunFacing()
        {
            // Get parent TowerSlot which has the pre-calculated facing direction
            var slot = GetComponentInParent<TowerSlot>();
            if (slot != null && slot.PathFacingDirection != Vector3.zero)
            {
                facingDirection = slot.PathFacingDirection;
                facingDirection.y = 0;
                facingDirection = facingDirection.normalized;

                // Set initial rotation
                if (turretHead != null)
                {
                    turretHead.rotation = Quaternion.LookRotation(facingDirection);
                }
            }
            else
            {
                // Fallback: face forward
                facingDirection = Vector3.forward;
            }
        }

        private void CreateVisual()
        {
            // Base
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.transform.SetParent(transform);
            baseObj.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            baseObj.transform.localScale = new Vector3(2.4f, 0.75f, 2.4f);

            var baseCollider = baseObj.GetComponent<Collider>();
            if (baseCollider != null) Destroy(baseCollider);

            var baseRenderer = baseObj.GetComponent<Renderer>();
            if (baseRenderer != null)
            {
                baseRenderer.material = Core.MaterialCache.CreateUnlit(data != null ? data.towerColor : Color.blue);
            }

            // Turret head
            var headObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headObj.name = "TurretHead";
            headObj.transform.SetParent(transform);
            headObj.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            headObj.transform.localScale = new Vector3(1.2f, 1.2f, 1.8f);
            turretHead = headObj.transform;

            var headCollider = headObj.GetComponent<Collider>();
            if (headCollider != null) Destroy(headCollider);

            var headRenderer = headObj.GetComponent<Renderer>();
            if (headRenderer != null)
            {
                headRenderer.material = Core.MaterialCache.CreateUnlit(data != null ? data.towerColor * 0.7f : Color.blue * 0.7f);
            }

            // Tower icon sprite (billboard above head)
            if (data != null && data.towerIcon != null)
            {
                var iconObj = new GameObject("TowerIcon");
                iconObj.transform.SetParent(transform);
                iconObj.transform.localPosition = new Vector3(0f, 3.8f, 0f);
                var sr = iconObj.AddComponent<SpriteRenderer>();
                sr.sprite = data.towerIcon;
                sr.color = Color.white;
                iconObj.AddComponent<TowerDefense.UI.BillboardSprite>();
            }

            // Range indicator (hidden by default)
            CreateRangeIndicator();
        }

        private void CreateRangeIndicator()
        {
            rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rangeIndicator.transform.SetParent(transform);
            rangeIndicator.transform.localPosition = Vector3.zero;

            rangeIndicator.transform.localScale = new Vector3(EffectiveRange * 2f, 0.01f, EffectiveRange * 2f);

            var collider = rangeIndicator.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = rangeIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = Core.MaterialCache.CreateUnlit(new Color(1f, 1f, 0f, 0.2f));
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

        private void CreateAuraIndicator()
        {
            auraIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            auraIndicator.name = "SlowAura";
            auraIndicator.transform.SetParent(transform);
            auraIndicator.transform.localPosition = Vector3.zero;

            auraIndicator.transform.localScale = new Vector3(EffectiveRange * 2f, 0.02f, EffectiveRange * 2f);

            var collider = auraIndicator.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = auraIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = Core.MaterialCache.CreateUnlit(new Color(0.2f, 0.7f, 0.7f, 0.3f));
            }

            // Aura is always visible for slow towers
            auraIndicator.SetActive(true);
        }

        private void UpdateAuraTower()
        {
            auraCheckTimer -= Time.deltaTime;
            if (auraCheckTimer > 0f) return;
            auraCheckTimer = 0.25f;

            var mgr = Core.EnemyManager.Instance;
            if (mgr == null) return;

            var enemies = mgr.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.IsDead) continue;

                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance <= EffectiveRange)
                {
                    enemy.ApplySlow(data.slowMultiplier, data.slowDuration);
                }
            }
        }

        private void Update()
        {
            if (data == null) return;

            fireCooldown -= Time.deltaTime;

            if (data.appliesSlow)
            {
                UpdateAuraTower();
            }
            else if (data.isShotgun)
            {
                UpdateShotgun();
            }
            else if (data.isTesla)
            {
                UpdateTeslaTower();
            }
            else if (data.isFlame)
            {
                UpdateFlameTower();
            }
            else
            {
                UpdateNormalTower();
            }
        }

        private void UpdateNormalTower()
        {
            FindTarget();

            if (currentTarget != null)
            {
                RotateTowardsTarget();

                if (fireCooldown <= 0f)
                {
                    Fire();
                    float speedBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.TowerSpeedBonus : 0f;
                    fireCooldown = 1f / (data.fireRate * (1f + speedBonus) * hasteMultiplier);
                }
            }
        }

        private void UpdateShotgun()
        {
            // Shotgun doesn't rotate - always faces the path
            // Check if any enemy is in range
            bool enemyInRange = HasEnemyInRange();

            if (enemyInRange && fireCooldown <= 0f)
            {
                FireShotgun();
                float speedBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.TowerSpeedBonus : 0f;
                fireCooldown = 1f / (data.fireRate * (1f + speedBonus) * hasteMultiplier);
            }
        }

        private void UpdateTeslaTower()
        {
            // Fade out chain visual
            if (teslaVisualTimer > 0f)
            {
                teslaVisualTimer -= Time.deltaTime;
                if (teslaVisualTimer <= 0f && teslaLineRenderer != null)
                {
                    teslaLineRenderer.positionCount = 0;
                }
            }

            FindTarget();

            if (currentTarget != null)
            {
                RotateTowardsTarget();

                if (fireCooldown <= 0f)
                {
                    FireTeslaChain();
                    float speedBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.TowerSpeedBonus : 0f;
                    fireCooldown = 1f / (data.fireRate * (1f + speedBonus) * hasteMultiplier);
                }
            }
        }

        private void FireTeslaChain()
        {
            if (currentTarget == null) return;

            float damageBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.TowerDamageBonus : 0f;
            float baseDamage = data.damage * (1f + damageBonus);
            int totalBounces = data.bounceCount + (UpgradeManager.Instance != null ? UpgradeManager.Instance.ExtraProjectiles : 0);

            // Build chain of targets (reuse collections)
            _reusableTeslaChain.Clear();
            _reusableTeslaHit.Clear();
            var chain = _reusableTeslaChain;
            var hit = _reusableTeslaHit;
            Enemy current = currentTarget;
            chain.Add(current);
            hit.Add(current);

            for (int i = 0; i < totalBounces; i++)
            {
                Enemy next = FindNearestUnhitEnemy(current.transform.position, data.bounceRange, hit);
                if (next == null) break;
                chain.Add(next);
                hit.Add(next);
                current = next;
            }

            // Apply damage with falloff
            float dmg = baseDamage;
            foreach (var enemy in chain)
            {
                if (!enemy.IsDead)
                    enemy.TakeDamage(dmg);
                dmg *= data.damageFalloff;
            }

            // Show chain lightning visual
            ShowTeslaChain(chain);
        }

        private Enemy FindNearestUnhitEnemy(Vector3 from, float range, HashSet<Enemy> exclude)
        {
            var mgr = Core.EnemyManager.Instance;
            if (mgr == null) return null;
            return mgr.GetClosestEnemyExcluding(from, range, exclude, data.canTargetFlying);
        }

        private void ShowTeslaChain(List<Enemy> chain)
        {
            if (teslaLineRenderer == null)
            {
                teslaLineRenderer = gameObject.AddComponent<LineRenderer>();
                teslaLineRenderer.startWidth = 0.15f;
                teslaLineRenderer.endWidth = 0.08f;
                teslaLineRenderer.material = Core.MaterialCache.CreateSpriteDefault();
                teslaLineRenderer.startColor = new Color(0.5f, 0.8f, 1f);
                teslaLineRenderer.endColor = new Color(0.3f, 0.5f, 1f, 0.4f);
            }

            // Chain starts from turret head, then through each enemy
            teslaLineRenderer.positionCount = chain.Count + 1;
            teslaLineRenderer.SetPosition(0, turretHead.position);
            for (int i = 0; i < chain.Count; i++)
            {
                Vector3 pos = chain[i].transform.position + Vector3.up * 0.5f;
                // Add slight random offset for a jagged lightning look
                pos += new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.1f, 0.1f), Random.Range(-0.3f, 0.3f));
                teslaLineRenderer.SetPosition(i + 1, pos);
            }

            teslaVisualTimer = 0.15f;
        }

        private void UpdateFlameTower()
        {
            FindTarget();

            if (currentTarget != null)
            {
                RotateTowardsTarget();

                if (fireCooldown <= 0f)
                {
                    FireFlame();
                    float speedBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.TowerSpeedBonus : 0f;
                    fireCooldown = 1f / (data.fireRate * (1f + speedBonus) * hasteMultiplier);
                }
            }
        }

        private void FireFlame()
        {
            if (currentTarget == null) return;

            float damageBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.TowerDamageBonus : 0f;
            float dps = data.fireDamagePerSecond * (1f + damageBonus);

            // Spawn fire patch at the enemy's current position on the path (y=0)
            Vector3 patchPos = currentTarget.transform.position;
            patchPos.y = 0.1f;

            var patch = FirePatch.GetFromPool(patchPos);
            patch.Initialize(dps, data.firePatchDuration, data.burnDuration, EffectiveRange * 0.3f);
        }

        private bool HasEnemyInRange()
        {
            var mgr = Core.EnemyManager.Instance;
            if (mgr == null) return false;
            return mgr.HasEnemyInRange(transform.position, EffectiveRange, data.canTargetFlying);
        }

        private void FireShotgun()
        {
            float damageBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.TowerDamageBonus : 0f;
            float actualDamage = data.damage * (1f + damageBonus);
            int projectileCount = data.shotgunProjectileCount + (UpgradeManager.Instance != null ? UpgradeManager.Instance.ExtraProjectiles : 0);

            float totalSpread = data.shotgunSpreadAngle;
            float angleStep = totalSpread / (projectileCount - 1);
            float startAngle = -totalSpread / 2f;

            for (int i = 0; i < projectileCount; i++)
            {
                float angle = startAngle + (angleStep * i);
                Vector3 direction = Quaternion.Euler(0, angle, 0) * facingDirection;

                SpawnPiercingProjectile(direction, actualDamage);
            }
        }

        private void SpawnPiercingProjectile(Vector3 direction, float damage)
        {
            // Calculate short lifetime - just enough to cross the path
            float travelDistance = EffectiveRange * 0.8f;
            float lifetime = travelDistance / data.projectileSpeed;

            var projectile = Projectile.GetFromPool(turretHead.position);
            projectile.InitializeDirectional(
                direction: direction,
                damage: damage,
                speed: data.projectileSpeed,
                color: data.towerColor,
                piercing: true,
                lifetime: lifetime
            );
        }

        private void FindTarget()
        {
            if (currentTarget != null)
            {
                float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
                bool invalidTarget = currentTarget.IsDead || distance > EffectiveRange;
                // Drop flying targets if this tower can't target them
                if (!invalidTarget && currentTarget.IsFlying && !data.canTargetFlying)
                    invalidTarget = true;
                if (invalidTarget)
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
            var mgr = Core.EnemyManager.Instance;
            if (mgr == null) return null;
            return mgr.GetClosestEnemyFiltered(transform.position, EffectiveRange, data.canTargetFlying, data.prioritizeFlying);
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

            float damageBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.TowerDamageBonus : 0f;
            float actualDamage = data.damage * (1f + damageBonus);
            int projectileCount = 1 + (UpgradeManager.Instance != null ? UpgradeManager.Instance.ExtraProjectiles : 0);

            // Get targets for projectiles
            List<Enemy> targets = GetTargetsForProjectiles(projectileCount);

            // Spawn projectiles toward each target
            foreach (var target in targets)
            {
                SpawnProjectile(target, actualDamage);
            }
        }

        private List<Enemy> GetTargetsForProjectiles(int count)
        {
            var mgr = Core.EnemyManager.Instance;
            if (mgr == null) return _reusableEnemyList;

            mgr.GetEnemiesInRange(transform.position, EffectiveRange, _reusableEnemyList, data.canTargetFlying);

            // Sort by distance (closest first)
            var pos = transform.position;
            _reusableEnemyList.Sort((a, b) =>
                Vector3.Distance(pos, a.transform.position)
                .CompareTo(Vector3.Distance(pos, b.transform.position)));

            // Trim to count
            if (_reusableEnemyList.Count > count)
                _reusableEnemyList.RemoveRange(count, _reusableEnemyList.Count - count);

            return _reusableEnemyList;
        }

        private void SpawnProjectile(Enemy target, float damage)
        {
            var projectile = Projectile.GetFromPool(turretHead.position);
            projectile.Initialize(
                target: target,
                damage: damage,
                speed: data.projectileSpeed,
                color: data.towerColor,
                isAreaDamage: data.isAreaDamage,
                areaRadius: data.areaRadius,
                appliesSlow: data.appliesSlow,
                slowMultiplier: data.slowMultiplier,
                slowDuration: data.slowDuration
            );
        }

        private void CacheTileCoord()
        {
            var slot = GetComponentInParent<TowerSlot>();
            if (slot != null && slot.ParentHex != null)
                cachedTileCoord = slot.ParentHex.Data.Coord;
        }

        public HexCoord? TileCoord => cachedTileCoord;

        public void SetTileCoord(HexCoord coord)
        {
            cachedTileCoord = coord;
        }

        public void SetFacingDirection(Vector3 dir)
        {
            dir.y = 0f;
            facingDirection = dir.normalized;
            if (turretHead != null && facingDirection != Vector3.zero)
                turretHead.rotation = Quaternion.LookRotation(facingDirection);
        }

        public void SetHasteMultiplier(float multiplier)
        {
            hasteMultiplier = multiplier;
        }

        private void OnDestroy()
        {
            if (teslaLineRenderer != null && teslaLineRenderer.material != null)
                Destroy(teslaLineRenderer.material);
        }

        private void OnDrawGizmos()
        {
            if (data != null && data.isShotgun && facingDirection != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, facingDirection * 2f);
            }
        }
    }
}