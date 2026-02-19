using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Grid;
using TowerDefense.Data;
using TowerDefense.UI;

namespace TowerDefense.Entities
{
    public class Enemy : MonoBehaviour
    {
        private EnemyData data;
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
        private bool isDying;
        private Color enemyColor;
        private EnemyType enemyType = EnemyType.Ground;
        private int currencyReward;
        private float invulnerabilityTimer;

        // Visuals
        private SpriteRenderer bodySprite;
        private Transform visualContainer;
        private Camera cachedCamera;

        // Walk animation
        private Sprite[] walkFramesDown;
        private Sprite[] walkFramesSide;
        private Sprite[] walkFramesUp;
        private Sprite[] currentWalkFrames;
        private int walkFrameIndex;
        private float walkFrameTimer;
        private float walkFrameInterval;
        private bool hasWalkAnimation;

        // Health bar (sprite-based)
        private Transform healthBarContainer;
        private SpriteRenderer healthBarFillSprite;

        // Shared white square sprite for placeholders / health bar
        private static Sprite _whiteSprite;
        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    var tex = new Texture2D(4, 4);
                    var pixels = new Color[16];
                    for (int i = 0; i < 16; i++) pixels[i] = Color.white;
                    tex.SetPixels(pixels);
                    tex.Apply();
                    _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                }
                return _whiteSprite;
            }
        }

        public float Health => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0 || isDying;
        public bool IsBoss => isBoss;
        public EnemyType EnemyType => enemyType;
        public bool IsFlying => enemyType == EnemyType.Flying;

        public event System.Action<Enemy> OnDeath;
        public event System.Action<Enemy> OnReachedCastle;

        public void Initialize(EnemyData enemyData, List<Vector3> path, int waveNumber, float healthMultiplier = 1f, float speedMultiplierConfig = 1f)
        {
            data = enemyData;
            enemyType = data.enemyType;
            currentWaypointIndex = 0;
            cachedWaveNumber = waveNumber;
            cachedHealthMultiplier = healthMultiplier;
            cachedSpeedMultiplier = speedMultiplierConfig;

            maxHealth = (data.baseHealth + (waveNumber - 1) * 5f) * healthMultiplier;
            currentHealth = maxHealth;
            float speedBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.EnemySpeedBonus : 0f;
            currentSpeed = (data.baseSpeed + (waveNumber - 1) * 0.1f) * speedMultiplierConfig * (1f + speedBonus);
            speedMultiplier = 1f;
            currencyReward = data.baseCurrencyReward + (waveNumber - 1) * 2;

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

            cachedCamera = Camera.main;

            SetupVisuals();
            CreateHealthBar();

            Core.EnemyManager.Instance?.Register(this);
        }

        private void SetupVisuals()
        {
            switch (enemyType)
            {
                case EnemyType.Flying:
                    enemyColor = new Color(0.9f, 0.6f, 0.1f);
                    break;
                case EnemyType.Cart:
                    enemyColor = new Color(0.55f, 0.35f, 0.15f);
                    break;
                case EnemyType.Goblin:
                    enemyColor = new Color(0.2f, 0.8f, 0.2f);
                    break;
                case EnemyType.Tank:
                    enemyColor = new Color(0.5f, 0.5f, 0.55f);
                    break;
                default:
                    enemyColor = Color.red;
                    break;
            }

            // Destroy existing mesh children from prefab
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.GetComponent<MeshRenderer>() != null)
                    Destroy(child.gameObject);
            }

            // Create billboard visual container
            visualContainer = new GameObject("Visual").transform;
            visualContainer.SetParent(transform);
            float yOffset = IsFlying ? data.flyHeight : 0.5f;
            visualContainer.localPosition = new Vector3(0f, yOffset, 0f);

            // Reuse existing SpriteRenderer child from prefab, or create one
            bodySprite = GetComponentInChildren<SpriteRenderer>();
            if (bodySprite != null)
            {
                bodySprite.transform.SetParent(visualContainer);
                bodySprite.transform.localPosition = Vector3.zero;
            }
            else
            {
                var spriteObj = new GameObject("Body");
                spriteObj.transform.SetParent(visualContainer);
                spriteObj.transform.localPosition = Vector3.zero;
                bodySprite = spriteObj.AddComponent<SpriteRenderer>();
            }
            // Setup walk animation
            walkFramesDown = data.walkFramesDown;
            walkFramesSide = data.walkFramesSide;
            walkFramesUp = data.walkFramesUp;
            hasWalkAnimation = (walkFramesDown != null && walkFramesDown.Length > 0)
                            || (walkFramesSide != null && walkFramesSide.Length > 0)
                            || (walkFramesUp != null && walkFramesUp.Length > 0);

            if (hasWalkAnimation)
            {
                walkFrameInterval = 1f / data.walkFrameRate;
                currentWalkFrames = walkFramesDown != null && walkFramesDown.Length > 0
                    ? walkFramesDown : (walkFramesSide != null && walkFramesSide.Length > 0 ? walkFramesSide : walkFramesUp);
                bodySprite.sprite = currentWalkFrames[0];
            }
            else
            {
                bodySprite.sprite = data.sprite != null ? data.sprite : WhiteSprite;
            }
        }

        private void CreateHealthBar()
        {
            healthBarContainer = new GameObject("HealthBar").transform;
            healthBarContainer.SetParent(visualContainer);
            healthBarContainer.localPosition = new Vector3(0f, 0.7f, 0f);
            healthBarContainer.localScale = Vector3.one;

            // Background (dark)
            var bgObj = new GameObject("HealthBarBG");
            bgObj.transform.SetParent(healthBarContainer);
            bgObj.transform.localPosition = Vector3.zero;
            bgObj.transform.localScale = new Vector3(1f, 0.12f, 1f);
            var bgSprite = bgObj.AddComponent<SpriteRenderer>();
            bgSprite.sprite = WhiteSprite;
            bgSprite.color = new Color(0.2f, 0.2f, 0.2f);
            bgSprite.sortingOrder = 10;

            // Fill (green/red)
            var fillObj = new GameObject("HealthBarFill");
            fillObj.transform.SetParent(healthBarContainer);
            fillObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            fillObj.transform.localScale = new Vector3(0.95f, 0.1f, 1f);
            healthBarFillSprite = fillObj.AddComponent<SpriteRenderer>();
            healthBarFillSprite.sprite = WhiteSprite;
            healthBarFillSprite.color = Color.green;
            healthBarFillSprite.sortingOrder = 11;

            // Start hidden (full health)
            healthBarContainer.gameObject.SetActive(false);
        }

        private void UpdateHealthBar()
        {
            if (healthBarContainer == null) return;

            float healthPercent = currentHealth / maxHealth;

            healthBarContainer.gameObject.SetActive(healthPercent < 1f);

            if (healthPercent < 1f && healthBarFillSprite != null)
            {
                healthBarFillSprite.transform.localScale = new Vector3(0.95f * healthPercent, 0.1f, 1f);
                healthBarFillSprite.transform.localPosition = new Vector3(-0.475f * (1f - healthPercent), 0f, -0.01f);
                healthBarFillSprite.color = Color.Lerp(Color.red, Color.green, healthPercent);
            }
        }

        private void Update()
        {
            if (isDying || IsDead || waypoints == null || waypoints.Count == 0)
                return;

            if (invulnerabilityTimer > 0f)
            {
                invulnerabilityTimer -= Time.deltaTime;
                return;
            }

            UpdateSlowEffect();
            UpdateBurnEffect();
            UpdateWalkAnimation();
            MoveAlongPath();
        }

        private void LateUpdate()
        {
            if (visualContainer != null && cachedCamera != null)
                visualContainer.rotation = cachedCamera.transform.rotation;
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

        private void UpdateWalkAnimation()
        {
            if (!hasWalkAnimation) return;

            // Pick direction based on movement
            if (currentWaypointIndex < waypoints.Count)
            {
                Vector3 dir = waypoints[currentWaypointIndex] - transform.position;
                float absX = Mathf.Abs(dir.x);
                float absZ = Mathf.Abs(dir.z);

                Sprite[] newFrames = currentWalkFrames;
                bool flipX = false;

                if (absX > absZ)
                {
                    // Primarily horizontal — use side frames
                    if (walkFramesSide != null && walkFramesSide.Length > 0)
                        newFrames = walkFramesSide;
                    flipX = dir.x > 0f;
                }
                else if (dir.z > 0f)
                {
                    if (walkFramesUp != null && walkFramesUp.Length > 0)
                        newFrames = walkFramesUp;
                }
                else
                {
                    if (walkFramesDown != null && walkFramesDown.Length > 0)
                        newFrames = walkFramesDown;
                }

                if (newFrames != currentWalkFrames)
                {
                    currentWalkFrames = newFrames;
                    walkFrameIndex = 0;
                    walkFrameTimer = 0f;
                }

                if (bodySprite != null)
                {
                    var s = bodySprite.transform.localScale;
                    s.x = flipX ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
                    bodySprite.transform.localScale = s;
                }
            }

            // Cycle frames
            walkFrameTimer += Time.deltaTime;
            if (walkFrameTimer >= walkFrameInterval)
            {
                walkFrameTimer -= walkFrameInterval;
                walkFrameIndex = (walkFrameIndex + 1) % currentWalkFrames.Length;
                if (bodySprite != null)
                    bodySprite.sprite = currentWalkFrames[walkFrameIndex];
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
            }
        }

        public void SetInvulnerable(float duration)
        {
            invulnerabilityTimer = duration;
        }

        public void TakeDamage(float damage)
        {
            if (isDying) return;
            if (invulnerabilityTimer > 0f) return;

            if (GameManager.Instance != null && GameManager.Instance.CheatInfiniteDamage)
                damage = currentHealth + 1f;

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

            transform.localScale = Vector3.one * 2f;

            if (bodySprite != null)
                bodySprite.color = new Color(0.5f, 0.1f, 0.6f);
        }

        public void ApplyBurn(float dps, float duration)
        {
            burnDps = dps;
            burnTimer = duration;
        }

        private void OnDestroy()
        {
            Core.EnemyManager.Instance?.Unregister(this);
        }

        private void Die()
        {
            if (isDying) return;
            isDying = true;

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

            Core.AudioManager.Instance?.PlayEnemyDeath(transform.position);

            // Spawn death burst particles (distinct from AoE ring)
            SpawnDeathBurst();

            OnDeath?.Invoke(this);
            StartCoroutine(DeathAnimation());
        }

        private void SpawnDeathBurst()
        {
            int count = isBoss ? 12 : 6;
            for (int i = 0; i < count; i++)
            {
                float size = Random.Range(0.2f, 0.5f);
                DeathBurstParticle.GetFromPool(transform.position, size, enemyColor);
            }
        }

        private IEnumerator DeathAnimation()
        {
            Vector3 startScale = transform.localScale;
            float duration = 0.25f;
            float elapsed = 0f;

            if (healthBarContainer != null)
                healthBarContainer.gameObject.SetActive(false);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

                float alpha = 1f - t;
                if (bodySprite != null)
                    bodySprite.color = new Color(enemyColor.r, enemyColor.g, enemyColor.b, alpha);

                yield return null;
            }

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

            var gm = GameManager.Instance;
            if (gm == null) return;

            var wm = gm.WaveManagerRef;
            for (int i = 0; i < 3; i++)
            {
                var path = new List<Vector3>(remaining);
                // Slight offset so they don't stack
                path[0] += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                var goblin = gm.SpawnEnemy(EnemyType.Goblin, path, cachedWaveNumber, cachedHealthMultiplier * 0.5f, cachedSpeedMultiplier * 1.1f);
                goblin.SetInvulnerable(0.5f);
                wm?.TrackEnemy(goblin);
            }
        }

        private void SpawnCurrencyPopup(int amount)
        {
            Sprite goldSprite = GameManager.Instance != null ? GameManager.Instance.GoldSprite : null;
            CurrencyPopup.GetFromPool(transform.position + Vector3.up * 2f, amount, goldSprite);
        }

        private void ReachCastle()
        {
            GameManager.Instance?.LoseLife();
            OnReachedCastle?.Invoke(this);
            Destroy(gameObject);
        }
    }

    public class DeathBurstParticle : MonoBehaviour
    {
        private static readonly Queue<DeathBurstParticle> pool = new Queue<DeathBurstParticle>();

        private Vector3 velocity;
        private float lifetime = 0.35f;
        private float timer;
        private Vector3 startScale;
        private Renderer rend;
        private MaterialPropertyBlock propBlock;
        private Color baseColor;
        private Material cachedMaterial;

        public static DeathBurstParticle GetFromPool(Vector3 position, float size, Color color)
        {
            DeathBurstParticle p = null;
            while (pool.Count > 0)
            {
                p = pool.Dequeue();
                if (p != null) break;
                p = null;
            }

            if (p == null)
            {
                var obj = Core.MaterialCache.CreatePrimitive(PrimitiveType.Sphere);
                obj.name = "DeathParticle";
                p = obj.AddComponent<DeathBurstParticle>();
                p.rend = obj.GetComponent<Renderer>();
                p.cachedMaterial = Core.MaterialCache.CreateUnlit(color);
                if (p.rend != null) p.rend.sharedMaterial = p.cachedMaterial;
                p.propBlock = Core.MaterialCache.GetPropertyBlock();
            }

            p.transform.position = position;
            p.transform.localScale = Vector3.one * size;
            p.gameObject.SetActive(true);
            p.Initialize(color);
            return p;
        }

        private void ReturnToPool()
        {
            gameObject.SetActive(false);
            pool.Enqueue(this);
        }

        public void Initialize(Color color)
        {
            velocity = new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(1f, 4f),
                Random.Range(-5f, 5f)
            );
            timer = 0f;
            startScale = transform.localScale;
            baseColor = color;
            if (cachedMaterial != null)
                cachedMaterial.color = color;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= lifetime)
            {
                ReturnToPool();
                return;
            }

            velocity.y -= 12f * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;

            float t = 1f - (timer / lifetime);
            transform.localScale = startScale * t;

            if (rend != null && propBlock != null)
            {
                propBlock.SetColor("_Color", new Color(baseColor.r, baseColor.g, baseColor.b, t));
                rend.SetPropertyBlock(propBlock);
            }
        }

        private void OnDestroy()
        {
            if (cachedMaterial != null)
                Destroy(cachedMaterial);
            if (propBlock != null)
            {
                Core.MaterialCache.ReturnPropertyBlock(propBlock);
                propBlock = null;
            }
        }
    }
}
