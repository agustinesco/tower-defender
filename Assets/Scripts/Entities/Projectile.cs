using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Core;

namespace TowerDefense.Entities
{
    public class Projectile : MonoBehaviour
    {
        private static readonly Queue<Projectile> pool = new Queue<Projectile>();

        private float speed;
        private Enemy target;
        private Vector3 targetLastPosition;
        private float damage;
        private bool isAreaDamage;
        private float areaRadius;
        private bool appliesSlow;
        private float slowMultiplier;
        private float slowDuration;
        private Color projectileColor;
        private float lifetime = 3f;

        // Piercing projectile support
        private bool isPiercing;
        private Vector3 moveDirection;
        private HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
        private float pierceRadius = 1.0f;
        private float pierceDamageFalloff = 0.5f;
        private bool canTargetFlying;

        // Fireball support
        private bool isFireball;
        private Vector3 fireballTarget;
        private float fireDps;
        private float firePatchDuration;
        private float fireBurnDuration;
        private float firePatchRadius;

        private TrailRenderer trail;
        private GameObject visual;

        public static Projectile GetFromPool(Vector3 position)
        {
            while (pool.Count > 0)
            {
                var p = pool.Dequeue();
                if (p != null)
                {
                    p.transform.position = position;
                    p.gameObject.SetActive(true);
                    return p;
                }
            }
            var obj = new GameObject("Projectile");
            obj.transform.position = position;
            return obj.AddComponent<Projectile>();
        }

        private void ReturnToPool()
        {
            target = null;
            isFireball = false;
            hitEnemies.Clear();
            gameObject.SetActive(false);
            if (trail != null)
                trail.Clear();
            pool.Enqueue(this);
        }

        public void Initialize(Enemy target, float damage, float speed, Color color,
            bool isAreaDamage = false, float areaRadius = 0f,
            bool appliesSlow = false, float slowMultiplier = 1f, float slowDuration = 0f)
        {
            this.target = target;
            this.targetLastPosition = target != null ? target.transform.position : transform.position;
            this.damage = damage;
            this.speed = speed;
            this.projectileColor = color;
            this.isAreaDamage = isAreaDamage;
            this.areaRadius = areaRadius;
            this.appliesSlow = appliesSlow;
            this.slowMultiplier = slowMultiplier;
            this.slowDuration = slowDuration;
            this.isPiercing = false;
            this.lifetime = 3f;

            CreateVisual();
        }

        public void InitializeDirectional(Vector3 direction, float damage, float speed, Color color, bool piercing = false, float lifetime = 3f, bool canTargetFlying = false)
        {
            this.target = null;
            this.moveDirection = direction.normalized;
            this.damage = damage;
            this.speed = speed;
            this.projectileColor = color;
            this.isPiercing = piercing;
            this.isAreaDamage = false;
            this.appliesSlow = false;
            this.lifetime = lifetime;
            this.canTargetFlying = canTargetFlying;

            CreateVisual();
        }

        public void InitializeFireball(Vector3 targetPos, float speed, Color color,
            float fireDps, float firePatchDuration, float fireBurnDuration, float firePatchRadius)
        {
            this.target = null;
            this.isFireball = true;
            this.fireballTarget = targetPos;
            this.damage = 0f;
            this.speed = speed;
            this.projectileColor = color;
            this.isPiercing = false;
            this.isAreaDamage = false;
            this.appliesSlow = false;
            this.lifetime = 3f;
            this.fireDps = fireDps;
            this.firePatchDuration = firePatchDuration;
            this.fireBurnDuration = fireBurnDuration;
            this.firePatchRadius = firePatchRadius;

            CreateVisual();
        }

        private void CreateVisual()
        {
            if (visual == null)
            {
                // Create sphere visual
                visual = MaterialCache.CreatePrimitive(PrimitiveType.Sphere);
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

                // Set initial material
                var renderer = visual.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material = Core.MaterialCache.CreateUnlit(projectileColor);
            }
            else
            {
                // Reuse: just update color
                var renderer = visual.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = projectileColor;
            }

            if (trail == null)
            {
                // Add trail renderer
                trail = gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.25f;
                trail.startWidth = 0.15f;
                trail.endWidth = 0f;
                trail.material = Core.MaterialCache.CreateSpriteDefault();
            }

            // Set trail color gradient
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(projectileColor, 0.0f),
                    new GradientColorKey(projectileColor, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            trail.colorGradient = gradient;
        }

        private void Update()
        {
            lifetime -= Time.deltaTime;
            if (lifetime <= 0f)
            {
                ReturnToPool();
                return;
            }

            if (isFireball)
            {
                UpdateFireball();
            }
            else if (isPiercing)
            {
                UpdatePiercing();
            }
            else
            {
                UpdateHoming();
            }
        }

        private void UpdateHoming()
        {
            // Update target position if target is still alive
            if (target != null && !target.IsDead)
            {
                targetLastPosition = target.transform.position;
            }

            // Move toward target position
            Vector3 direction = (targetLastPosition - transform.position).normalized;
            float moveDistance = speed * Time.deltaTime;
            float distanceToTarget = Vector3.Distance(transform.position, targetLastPosition);

            if (moveDistance >= distanceToTarget)
            {
                // Reached destination
                transform.position = targetLastPosition;
                OnReachDestination();
            }
            else
            {
                transform.position += direction * moveDistance;
            }
        }

        private void UpdatePiercing()
        {
            // Move in fixed direction
            transform.position += moveDirection * speed * Time.deltaTime;

            var mgr = Core.EnemyManager.Instance;
            if (mgr == null) return;

            var enemies = mgr.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.IsDead) continue;
                if (hitEnemies.Contains(enemy)) continue;
                if (enemy.IsFlying && !canTargetFlying) continue;

                Vector3 enemyPos = enemy.transform.position;
                float dx = transform.position.x - enemyPos.x;
                float dz = transform.position.z - enemyPos.z;
                float distXZ = Mathf.Sqrt(dx * dx + dz * dz);
                if (distXZ <= pierceRadius)
                {
                    enemy.TakeDamage(damage);
                    damage *= pierceDamageFalloff;
                    hitEnemies.Add(enemy);
                }
            }
        }

        private void UpdateFireball()
        {
            Vector3 direction = (fireballTarget - transform.position).normalized;
            float moveDistance = speed * Time.deltaTime;
            float distanceToTarget = Vector3.Distance(transform.position, fireballTarget);

            if (moveDistance >= distanceToTarget)
            {
                transform.position = fireballTarget;
                // Spawn fire patch at impact point
                Vector3 patchPos = fireballTarget;
                patchPos.y = 0.1f;
                var patch = FirePatch.GetFromPool(patchPos);
                patch.Initialize(fireDps, firePatchDuration, fireBurnDuration, firePatchRadius);
                ReturnToPool();
            }
            else
            {
                transform.position += direction * moveDistance;
            }
        }

        private void OnReachDestination()
        {
            if (appliesSlow)
            {
                // Slow tower projectile
                if (target != null && !target.IsDead)
                {
                    target.ApplySlow(slowMultiplier, slowDuration);
                }
            }
            else if (isAreaDamage)
            {
                // Area damage - hit all enemies in radius
                var mgr = Core.EnemyManager.Instance;
                if (mgr != null)
                {
                    var enemies = mgr.ActiveEnemies;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        var enemy = enemies[i];
                        if (enemy == null || enemy.IsDead) continue;
                        if (enemy.IsFlying) continue;

                        float distance = Vector3.Distance(transform.position, enemy.transform.position);
                        if (distance <= areaRadius)
                        {
                            enemy.TakeDamage(damage);
                        }
                    }
                }

                // Spawn AoE circle indicator
                SpawnAoeIndicator();
            }
            else
            {
                // Single target damage
                if (target != null && !target.IsDead)
                {
                    target.TakeDamage(damage);
                }
            }

            ReturnToPool();
        }

        private void SpawnAoeIndicator()
        {
            var indicatorObj = new GameObject("AoeIndicator");
            indicatorObj.transform.position = new Vector3(transform.position.x, 0.15f, transform.position.z);
            var indicator = indicatorObj.AddComponent<AoeIndicator>();
            indicator.Initialize(areaRadius, projectileColor);
        }
    }

    public class AoeIndicator : MonoBehaviour
    {
        private float radius;
        private float duration = 0.5f;
        private float timer;
        private LineRenderer lineRenderer;
        private Color baseColor;

        public void Initialize(float radius, Color color)
        {
            this.radius = radius;
            this.timer = duration;
            this.baseColor = color;

            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.3f;
            lineRenderer.endWidth = 0.3f;
            lineRenderer.positionCount = 32;
            lineRenderer.material = Core.MaterialCache.CreateUnlit(color);

            UpdateCircle(0f);
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            float progress = 1f - (timer / duration);
            UpdateCircle(progress);
        }

        private void UpdateCircle(float progress)
        {
            float currentRadius = radius * Mathf.Min(progress * 3f, 1f);
            float alpha = timer / duration;
            var fadeColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            lineRenderer.startColor = fadeColor;
            lineRenderer.endColor = fadeColor;
            float width = 0.3f * alpha;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;

            Vector3 center = transform.position;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * (360f / 32f) * Mathf.Deg2Rad;
                lineRenderer.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(angle) * currentRadius,
                    center.y,
                    center.z + Mathf.Sin(angle) * currentRadius
                ));
            }
        }

        private void OnDestroy()
        {
            if (lineRenderer != null && lineRenderer.material != null)
                Destroy(lineRenderer.material);
        }
    }
}
