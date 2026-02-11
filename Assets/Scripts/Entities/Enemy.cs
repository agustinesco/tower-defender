using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Grid;
using TowerDefense.Data;
using TowerDefense.UI;

namespace TowerDefense.Entities
{
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private float baseSpeed = 2f;
        [SerializeField] private float baseHealth = 10f;
        [SerializeField] private int currencyReward = 10;

        private List<Vector3> waypoints;
        private int currentWaypointIndex;
        private int cachedWaveNumber = 1;
        private float cachedHealthMultiplier = 1f;
        private float cachedSpeedMultiplier = 1f;
        private float currentHealth;
        private float maxHealth;
        private float currentSpeed;
        private float speedMultiplier = 1f;
        private float slowTimer;
        private float burnTimer;
        private float burnDps;
        private float goldMultiplier = 1f;
        private bool isBoss;
        private EnemyType enemyType = EnemyType.Ground;
        private float flyHeight = 4f;

        // Health bar
        private GameObject healthBarBackground;
        private GameObject healthBarFill;
        private Transform healthBarContainer;
        private Renderer healthBarFillRenderer;
        private MaterialPropertyBlock healthBarPropBlock;

        public float Health => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0;
        public bool IsBoss => isBoss;
        public EnemyType EnemyType => enemyType;
        public bool IsFlying => enemyType == EnemyType.Flying;

        public event System.Action<Enemy> OnDeath;
        public event System.Action<Enemy> OnReachedCastle;

        public void Initialize(List<Vector3> path, int waveNumber, float healthMultiplier = 1f, float speedMultiplierConfig = 1f, EnemyType type = EnemyType.Ground)
        {
            enemyType = type;
            currentWaypointIndex = 0;
            cachedWaveNumber = waveNumber;
            cachedHealthMultiplier = healthMultiplier;
            cachedSpeedMultiplier = speedMultiplierConfig;
            maxHealth = (baseHealth + (waveNumber - 1) * 5f) * healthMultiplier;
            currentHealth = maxHealth;
            float speedBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.EnemySpeedBonus : 0f;
            currentSpeed = (baseSpeed + (waveNumber - 1) * 0.1f) * speedMultiplierConfig * (1f + speedBonus);
            speedMultiplier = 1f;
            currencyReward = 10 + (waveNumber - 1) * 2;

            if (IsFlying)
            {
                currentSpeed *= 1.3f;
                currencyReward = Mathf.RoundToInt(currencyReward * 1.5f);
            }

            if (enemyType == EnemyType.Cart)
            {
                maxHealth *= 2f;
                currentHealth = maxHealth;
                currentSpeed *= 0.6f;
                currencyReward = Mathf.RoundToInt(currencyReward * 1.2f);
            }

            // Apply random perpendicular offset to ALL waypoints so enemy maintains lane
            float randomOffset = Random.Range(-3.0f, 3.0f);
            waypoints = new List<Vector3>();

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 waypoint = path[i];

                // Calculate path direction at this point
                Vector3 pathDir;
                if (i < path.Count - 1)
                {
                    pathDir = (path[i + 1] - path[i]).normalized;
                }
                else if (i > 0)
                {
                    pathDir = (path[i] - path[i - 1]).normalized;
                }
                else
                {
                    pathDir = Vector3.forward;
                }

                // Apply perpendicular offset
                Vector3 perpendicular = new Vector3(-pathDir.z, 0f, pathDir.x);
                waypoints.Add(waypoint + perpendicular * randomOffset);
            }

            if (waypoints.Count > 0)
            {
                transform.position = waypoints[0];
            }

            healthBarPropBlock = Core.MaterialCache.GetPropertyBlock();

            CreateVisual();
            CreateHealthBar();

            Core.EnemyManager.Instance?.Register(this);
        }

        private void CreateVisual()
        {
            // If a "Body" child already exists (from prefab), keep prefab visuals as-is
            if (transform.Find("Body") != null)
                return;

            // Fallback: create visuals from scratch
            if (enemyType == EnemyType.Cart)
                CreateCartVisual();
            else if (IsFlying)
                CreateFlyingVisual();
            else
                CreateGroundVisual();
        }

        private void ApplyMaterials()
        {
            if (IsFlying)
            {
                ApplyMaterialToChild("Body", new Color(0.9f, 0.6f, 0.1f));
                ApplyMaterialToChild("LeftWing", new Color(0.8f, 0.5f, 0.05f));
                ApplyMaterialToChild("RightWing", new Color(0.8f, 0.5f, 0.05f));
            }
            else
            {
                ApplyMaterialToChild("Body", Color.red);
            }
        }

        private void ApplyMaterialToChild(string childName, Color color)
        {
            var child = transform.Find(childName);
            if (child != null)
            {
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material = Core.MaterialCache.CreateUnlit(color);
            }
        }

        private void CreateGroundVisual()
        {
            var visual = MaterialCache.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Body";
            visual.transform.SetParent(transform);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = Core.MaterialCache.CreateUnlit(Color.red);
            }

            var sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.4f;
            sphereCollider.center = new Vector3(0f, 0.5f, 0f);
        }

        private void CreateFlyingVisual()
        {
            // Body - flattened sphere
            var body = MaterialCache.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(transform);
            body.transform.localPosition = new Vector3(0f, flyHeight, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f);

            var bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
                bodyRenderer.material = Core.MaterialCache.CreateUnlit(new Color(0.9f, 0.6f, 0.1f));

            // Left wing
            var leftWing = MaterialCache.CreatePrimitive(PrimitiveType.Cube);
            leftWing.name = "LeftWing";
            leftWing.transform.SetParent(transform);
            leftWing.transform.localPosition = new Vector3(-0.5f, flyHeight + 0.1f, 0f);
            leftWing.transform.localScale = new Vector3(0.6f, 0.05f, 0.4f);
            leftWing.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);

            var lwR = leftWing.GetComponent<Renderer>();
            if (lwR != null)
                lwR.material = Core.MaterialCache.CreateUnlit(new Color(0.8f, 0.5f, 0.05f));

            // Right wing
            var rightWing = MaterialCache.CreatePrimitive(PrimitiveType.Cube);
            rightWing.name = "RightWing";
            rightWing.transform.SetParent(transform);
            rightWing.transform.localPosition = new Vector3(0.5f, flyHeight + 0.1f, 0f);
            rightWing.transform.localScale = new Vector3(0.6f, 0.05f, 0.4f);
            rightWing.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);

            var rwR = rightWing.GetComponent<Renderer>();
            if (rwR != null)
                rwR.material = Core.MaterialCache.CreateUnlit(new Color(0.8f, 0.5f, 0.05f));

            var sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.5f;
            sphereCollider.center = new Vector3(0f, flyHeight, 0f);
        }

        private void CreateCartVisual()
        {
            // Body - large box (wagon)
            var body = MaterialCache.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(transform);
            body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            body.transform.localScale = new Vector3(1.2f, 0.8f, 1.8f);

            var bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
                bodyRenderer.material = Core.MaterialCache.CreateUnlit(new Color(0.55f, 0.35f, 0.15f));

            // Roof
            var roof = MaterialCache.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(transform);
            roof.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            roof.transform.localScale = new Vector3(1.3f, 0.15f, 2f);
            var roofR = roof.GetComponent<Renderer>();
            if (roofR != null)
                roofR.material = Core.MaterialCache.CreateUnlit(new Color(0.4f, 0.25f, 0.1f));

            // Front wheel (left)
            CreateWheel(new Vector3(-0.7f, 0.25f, 0.6f));
            // Front wheel (right)
            CreateWheel(new Vector3(0.7f, 0.25f, 0.6f));
            // Rear wheel (left)
            CreateWheel(new Vector3(-0.7f, 0.25f, -0.6f));
            // Rear wheel (right)
            CreateWheel(new Vector3(0.7f, 0.25f, -0.6f));

            // Scale up the whole enemy
            transform.localScale = Vector3.one * 1.5f;

            var sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.6f;
            sphereCollider.center = new Vector3(0f, 0.7f, 0f);
        }

        private void CreateWheel(Vector3 localPos)
        {
            var wheel = MaterialCache.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Wheel";
            wheel.transform.SetParent(transform);
            wheel.transform.localPosition = localPos;
            wheel.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var r = wheel.GetComponent<Renderer>();
            if (r != null)
                r.material = Core.MaterialCache.CreateUnlit(new Color(0.3f, 0.2f, 0.1f));
        }

        private void CreateHealthBar()
        {
            // Container positioned in front of enemy, facing up for top-down view
            healthBarContainer = new GameObject("HealthBar").transform;
            healthBarContainer.SetParent(transform);
            float hbY = IsFlying ? flyHeight - 0.4f : 0.1f;
            float hbZ = IsFlying ? 0.9f : 0.8f;
            healthBarContainer.localPosition = new Vector3(0f, hbY, hbZ);
            healthBarContainer.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Background (dark)
            healthBarBackground = MaterialCache.CreatePrimitive(PrimitiveType.Cube);
            healthBarBackground.name = "HealthBarBG";
            healthBarBackground.transform.SetParent(healthBarContainer);
            healthBarBackground.transform.localPosition = Vector3.zero;
            healthBarBackground.transform.localScale = new Vector3(1f, 0.15f, 0.25f);

            var bgRenderer = healthBarBackground.GetComponent<Renderer>();
            if (bgRenderer != null)
            {
                bgRenderer.material = Core.MaterialCache.CreateUnlit(new Color(0.2f, 0.2f, 0.2f));
            }

            // Fill (green/red)
            healthBarFill = MaterialCache.CreatePrimitive(PrimitiveType.Cube);
            healthBarFill.name = "HealthBarFill";
            healthBarFill.transform.SetParent(healthBarContainer);
            healthBarFill.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            healthBarFill.transform.localScale = new Vector3(0.95f, 0.12f, 0.22f);

            healthBarFillRenderer = healthBarFill.GetComponent<Renderer>();
            if (healthBarFillRenderer != null)
            {
                healthBarFillRenderer.material = Core.MaterialCache.CreateUnlit(Color.green);
            }

            // Start hidden (full health)
            healthBarContainer.gameObject.SetActive(false);
        }

        private void UpdateHealthBar()
        {
            if (healthBarContainer == null) return;

            float healthPercent = currentHealth / maxHealth;

            // Show only when damaged
            healthBarContainer.gameObject.SetActive(healthPercent < 1f);

            if (healthPercent < 1f)
            {
                // Update fill scale and position (anchored on left, empties to the right)
                healthBarFill.transform.localScale = new Vector3(0.95f * healthPercent, 0.12f, 0.22f);
                healthBarFill.transform.localPosition = new Vector3(0.475f * (1f - healthPercent), 0f, -0.05f);

                // Color: green to red based on health
                if (healthBarFillRenderer != null && healthBarPropBlock != null)
                {
                    healthBarPropBlock.SetColor("_Color", Color.Lerp(Color.red, Color.green, healthPercent));
                    healthBarFillRenderer.SetPropertyBlock(healthBarPropBlock);
                }
            }
        }

        private void Update()
        {
            if (IsDead || waypoints == null || waypoints.Count == 0)
                return;

            UpdateSlowEffect();
            UpdateBurnEffect();
            MoveAlongPath();
        }

        private void UpdateSlowEffect()
        {
            if (slowTimer > 0)
            {
                slowTimer -= Time.deltaTime;
                if (slowTimer <= 0)
                {
                    speedMultiplier = 1f;
                }
            }
        }

        private void UpdateBurnEffect()
        {
            if (burnTimer > 0f)
            {
                burnTimer -= Time.deltaTime;
                TakeDamage(burnDps * Time.deltaTime);
            }
        }

        private void MoveAlongPath()
        {
            if (currentWaypointIndex >= waypoints.Count)
            {
                ReachCastle();
                return;
            }

            Vector3 target = waypoints[currentWaypointIndex];
            Vector3 direction = (target - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, target);
            float moveDistance = currentSpeed * speedMultiplier * Time.deltaTime;

            if (moveDistance >= distance)
            {
                transform.position = target;
                currentWaypointIndex++;
            }
            else
            {
                transform.position += direction * moveDistance;

                // Face movement direction
                if (direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }
        }

        public void TakeDamage(float damage)
        {
            currentHealth -= damage;
            UpdateHealthBar();

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        public void ApplySlow(float multiplier, float duration)
        {
            speedMultiplier = multiplier;
            slowTimer = duration;
        }

        public void SetGoldMultiplier(float multiplier)
        {
            goldMultiplier = multiplier;
        }

        public void MakeBoss()
        {
            isBoss = true;
            maxHealth *= 10f;
            currentHealth = maxHealth;
            currentSpeed *= 0.6f;
            currencyReward = 200;

            // Scale up visual
            transform.localScale = Vector3.one * 2f;

            // Recolor to dark purple (skip health bar children)
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.gameObject.name == "HealthBarBG" || r.gameObject.name == "HealthBarFill")
                    continue;
                r.material.color = new Color(0.5f, 0.1f, 0.6f);
            }
        }

        public void ApplyBurn(float dps, float duration)
        {
            burnDps = dps;
            burnTimer = duration;
        }

        private void OnDestroy()
        {
            Core.EnemyManager.Instance?.Unregister(this);
            if (healthBarPropBlock != null)
            {
                Core.MaterialCache.ReturnPropertyBlock(healthBarPropBlock);
                healthBarPropBlock = null;
            }
        }

        private void Die()
        {
            float goldBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.EnemyGoldBonus : 0f;
            float goldenTouchMul = GameManager.Instance != null ? GameManager.Instance.GetGoldenTouchMultiplierAt(transform.position) : 1f;
            int actualReward = Mathf.RoundToInt(currencyReward * (1f + goldBonus) * goldMultiplier * goldenTouchMul);
            GameManager.Instance?.AddCurrency(actualReward);

            if (isBoss && PersistenceManager.Instance != null)
            {
                var resourceTypes = (ResourceType[])System.Enum.GetValues(typeof(ResourceType));
                foreach (var rt in resourceTypes)
                    PersistenceManager.Instance.AddRunResource(rt, 3);
            }

            // Cart enemies spawn 3 goblins on death
            if (enemyType == EnemyType.Cart)
                SpawnCartGoblins();

            // Spawn currency popup
            SpawnCurrencyPopup(actualReward);

            OnDeath?.Invoke(this);
            Destroy(gameObject);
        }

        private void SpawnCartGoblins()
        {
            // Build remaining path from current position
            var remaining = new List<Vector3>();
            remaining.Add(transform.position);
            if (waypoints != null)
            {
                for (int i = currentWaypointIndex; i < waypoints.Count; i++)
                    remaining.Add(waypoints[i]);
            }
            if (remaining.Count < 2) return;

            var wm = Object.FindFirstObjectByType<Core.WaveManager>();
            for (int i = 0; i < 3; i++)
            {
                var goblinObj = new GameObject("CartGoblin");
                var goblin = goblinObj.AddComponent<Enemy>();
                var path = new List<Vector3>(remaining);
                // Slight offset so they don't stack
                path[0] += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                goblin.Initialize(path, cachedWaveNumber, cachedHealthMultiplier * 0.5f, cachedSpeedMultiplier * 1.1f, EnemyType.Ground);
                wm?.TrackEnemy(goblin);
            }
        }

        private void SpawnCurrencyPopup(int amount)
        {
            var popupObj = new GameObject("CurrencyPopup");
            popupObj.transform.position = transform.position + Vector3.up * 2f;

            var popup = popupObj.AddComponent<CurrencyPopup>();
            Sprite goldSprite = GameManager.Instance != null ? GameManager.Instance.GoldSprite : null;
            popup.Initialize(amount, goldSprite);
        }

        private void ReachCastle()
        {
            GameManager.Instance?.LoseLife();
            OnReachedCastle?.Invoke(this);
            Destroy(gameObject);
        }
    }
}