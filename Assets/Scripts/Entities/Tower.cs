using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Grid;

namespace TowerDefense.Entities
{
    public class Tower : MonoBehaviour
    {
        private TowerData data;
        private float fireCooldown;
        private Enemy currentTarget;
        private GameObject rangeIndicator;
        private Transform turretHead;
        private Vector3 facingDirection; // For shotgun towers
        private GameObject auraIndicator; // For slow aura towers
        private LineRenderer teslaLineRenderer; // For tesla chain lightning
        private float teslaVisualTimer; // How long to show the chain

        public TowerData Data => data;
        public int SellValue => data != null ? data.cost / 2 : 0;

        public void Initialize(TowerData towerData)
        {
            data = towerData;
            fireCooldown = 0f;
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

        private void CreateAuraIndicator()
        {
            auraIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            auraIndicator.name = "SlowAura";
            auraIndicator.transform.SetParent(transform);
            auraIndicator.transform.localPosition = Vector3.zero;

            float range = data != null ? data.range : 3f;
            auraIndicator.transform.localScale = new Vector3(range * 2f, 0.02f, range * 2f);

            var collider = auraIndicator.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = auraIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Unlit/Color"));
                // Cyan/teal color matching the slow tower, semi-transparent
                renderer.material.color = new Color(0.2f, 0.7f, 0.7f, 0.3f);
            }

            // Aura is always visible for slow towers
            auraIndicator.SetActive(true);
        }

        private void UpdateAuraTower()
        {
            // Slow aura doesn't rotate or fire projectiles
            // Instead, it continuously slows all enemies in range
            var enemies = FindObjectsOfType<Enemy>();
            foreach (var enemy in enemies)
            {
                if (enemy.IsDead) continue;

                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance <= data.range)
                {
                    // Apply slow - the slowDuration acts as linger time after leaving
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
                    fireCooldown = 1f / (data.fireRate * (1f + speedBonus));
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
                fireCooldown = 1f / (data.fireRate * (1f + speedBonus));
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
                    fireCooldown = 1f / (data.fireRate * (1f + speedBonus));
                }
            }
        }

        private void FireTeslaChain()
        {
            if (currentTarget == null) return;

            float damageBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.TowerDamageBonus : 0f;
            float baseDamage = data.damage * (1f + damageBonus);
            int totalBounces = data.bounceCount + (UpgradeManager.Instance != null ? UpgradeManager.Instance.ExtraProjectiles : 0);

            // Build chain of targets
            List<Enemy> chain = new List<Enemy>();
            HashSet<Enemy> hit = new HashSet<Enemy>();
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
            var enemies = FindObjectsOfType<Enemy>();
            Enemy closest = null;
            float closestDist = range;

            foreach (var enemy in enemies)
            {
                if (enemy.IsDead || exclude.Contains(enemy)) continue;
                float dist = Vector3.Distance(from, enemy.transform.position);
                if (dist <= closestDist)
                {
                    closest = enemy;
                    closestDist = dist;
                }
            }

            return closest;
        }

        private void ShowTeslaChain(List<Enemy> chain)
        {
            if (teslaLineRenderer == null)
            {
                teslaLineRenderer = gameObject.AddComponent<LineRenderer>();
                teslaLineRenderer.startWidth = 0.15f;
                teslaLineRenderer.endWidth = 0.08f;
                teslaLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
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
                    fireCooldown = 1f / (data.fireRate * (1f + speedBonus));
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

            GameObject patchObj = new GameObject("FirePatch");
            patchObj.transform.position = patchPos;
            var patch = patchObj.AddComponent<FirePatch>();
            patch.Initialize(dps, data.firePatchDuration, data.burnDuration, data.range * 0.3f);
        }

        private bool HasEnemyInRange()
        {
            var enemies = FindObjectsOfType<Enemy>();
            foreach (var enemy in enemies)
            {
                if (enemy.IsDead) continue;
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance <= data.range)
                {
                    return true;
                }
            }
            return false;
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
            GameObject projectileObj = new GameObject("ShotgunProjectile");
            projectileObj.transform.position = turretHead.position;

            // Calculate short lifetime - just enough to cross the path
            // Use range as the travel distance, add small buffer
            float travelDistance = data.range * 0.8f; // Slightly less than range
            float lifetime = travelDistance / data.projectileSpeed;

            var projectile = projectileObj.AddComponent<Projectile>();
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
            var enemies = FindObjectsOfType<Enemy>();
            List<Enemy> validTargets = new List<Enemy>();

            foreach (var enemy in enemies)
            {
                if (!enemy.IsDead)
                {
                    float distance = Vector3.Distance(transform.position, enemy.transform.position);
                    if (distance <= data.range)
                    {
                        validTargets.Add(enemy);
                    }
                }
            }

            // Sort by distance (closest first)
            validTargets.Sort((a, b) =>
                Vector3.Distance(transform.position, a.transform.position)
                .CompareTo(Vector3.Distance(transform.position, b.transform.position)));

            // Return up to 'count' targets
            List<Enemy> result = new List<Enemy>();
            for (int i = 0; i < Mathf.Min(count, validTargets.Count); i++)
            {
                result.Add(validTargets[i]);
            }

            return result;
        }

        private void SpawnProjectile(Enemy target, float damage)
        {
            GameObject projectileObj = new GameObject("Projectile");
            projectileObj.transform.position = turretHead.position;

            var projectile = projectileObj.AddComponent<Projectile>();
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