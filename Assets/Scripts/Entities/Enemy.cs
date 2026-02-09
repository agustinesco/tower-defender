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
        private float currentHealth;
        private float maxHealth;
        private float currentSpeed;
        private float speedMultiplier = 1f;
        private float slowTimer;
        private float burnTimer;
        private float burnDps;
        private bool isTargetingMine;
        private HexCoord mineTargetCoord;
        private float goldMultiplier = 1f;
        private bool isBoss;

        // Health bar
        private GameObject healthBarBackground;
        private GameObject healthBarFill;
        private Transform healthBarContainer;

        public float Health => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0;
        public bool IsBoss => isBoss;

        public event System.Action<Enemy> OnDeath;
        public event System.Action<Enemy> OnReachedCastle;

        public void Initialize(List<Vector3> path, int waveNumber, float healthMultiplier = 1f, float speedMultiplierConfig = 1f)
        {
            currentWaypointIndex = 0;
            maxHealth = (baseHealth + (waveNumber - 1) * 5f) * healthMultiplier;
            currentHealth = maxHealth;
            float speedBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.EnemySpeedBonus : 0f;
            currentSpeed = (baseSpeed + (waveNumber - 1) * 0.1f) * speedMultiplierConfig * (1f + speedBonus);
            speedMultiplier = 1f;
            currencyReward = 10 + (waveNumber - 1) * 2;

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

            CreateVisual();
            CreateHealthBar();
        }

        private void CreateVisual()
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.transform.SetParent(transform);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            var collider = visual.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Unlit/Color"));
                renderer.material.color = Color.red;
            }

            // Add collider to enemy for targeting
            var sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.4f;
            sphereCollider.center = new Vector3(0f, 0.5f, 0f);
        }

        private void CreateHealthBar()
        {
            // Container positioned in front of enemy, facing up for top-down view
            healthBarContainer = new GameObject("HealthBar").transform;
            healthBarContainer.SetParent(transform);
            healthBarContainer.localPosition = new Vector3(0f, 0.1f, 0.8f);
            healthBarContainer.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Background (dark)
            healthBarBackground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            healthBarBackground.name = "HealthBarBG";
            healthBarBackground.transform.SetParent(healthBarContainer);
            healthBarBackground.transform.localPosition = Vector3.zero;
            healthBarBackground.transform.localScale = new Vector3(1f, 0.15f, 0.25f);

            var bgCollider = healthBarBackground.GetComponent<Collider>();
            if (bgCollider != null) Destroy(bgCollider);

            var bgRenderer = healthBarBackground.GetComponent<Renderer>();
            if (bgRenderer != null)
            {
                bgRenderer.material = new Material(Shader.Find("Unlit/Color"));
                bgRenderer.material.color = new Color(0.2f, 0.2f, 0.2f);
            }

            // Fill (green/red)
            healthBarFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            healthBarFill.name = "HealthBarFill";
            healthBarFill.transform.SetParent(healthBarContainer);
            healthBarFill.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            healthBarFill.transform.localScale = new Vector3(0.95f, 0.12f, 0.22f);

            var fillCollider = healthBarFill.GetComponent<Collider>();
            if (fillCollider != null) Destroy(fillCollider);

            var fillRenderer = healthBarFill.GetComponent<Renderer>();
            if (fillRenderer != null)
            {
                fillRenderer.material = new Material(Shader.Find("Unlit/Color"));
                fillRenderer.material.color = Color.green;
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
                var fillRenderer = healthBarFill.GetComponent<Renderer>();
                if (fillRenderer != null)
                {
                    fillRenderer.material.color = Color.Lerp(Color.red, Color.green, healthPercent);
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

        public void SetMineTarget(HexCoord coord)
        {
            isTargetingMine = true;
            mineTargetCoord = coord;
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

            // Recolor to dark purple
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.gameObject.name != "HealthBarBG" && r.gameObject.name != "HealthBarFill")
                    r.material.color = new Color(0.5f, 0.1f, 0.6f);
            }
        }

        public void ApplyBurn(float dps, float duration)
        {
            burnDps = dps;
            burnTimer = duration;
        }

        private void Die()
        {
            float goldBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.EnemyGoldBonus : 0f;
            int actualReward = Mathf.RoundToInt(currencyReward * (1f + goldBonus) * goldMultiplier);
            GameManager.Instance?.AddCurrency(actualReward);

            if (isBoss && PersistenceManager.Instance != null)
            {
                var resourceTypes = (ResourceType[])System.Enum.GetValues(typeof(ResourceType));
                foreach (var rt in resourceTypes)
                    PersistenceManager.Instance.AddRunResource(rt, 3);
            }

            // Spawn currency popup
            SpawnCurrencyPopup(actualReward);

            OnDeath?.Invoke(this);
            Destroy(gameObject);
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
            if (isTargetingMine)
            {
                GameManager.Instance?.DamageMiningOutpost(mineTargetCoord);
            }
            else
            {
                GameManager.Instance?.LoseLife();
            }
            OnReachedCastle?.Invoke(this);
            Destroy(gameObject);
        }
    }
}