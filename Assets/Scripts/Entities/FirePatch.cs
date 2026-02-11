using UnityEngine;
using TowerDefense.Core;

namespace TowerDefense.Entities
{
    public class FirePatch : MonoBehaviour
    {
        private static readonly System.Collections.Generic.Queue<FirePatch> pool = new System.Collections.Generic.Queue<FirePatch>();

        private float damagePerSecond;
        private float duration;
        private float burnDuration;
        private float radius;
        private float timer;
        private GameObject visual;
        private Renderer visualRenderer;
        private MaterialPropertyBlock propBlock;

        public static FirePatch GetFromPool(Vector3 position)
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
            var obj = new GameObject("FirePatch");
            obj.transform.position = position;
            return obj.AddComponent<FirePatch>();
        }

        private void ReturnToPool()
        {
            gameObject.SetActive(false);
            pool.Enqueue(this);
        }

        public void Initialize(float dps, float duration, float burnDuration, float radius)
        {
            this.damagePerSecond = dps;
            this.duration = duration;
            this.burnDuration = burnDuration;
            this.radius = radius;
            this.timer = duration;
            propBlock = MaterialCache.GetPropertyBlock();

            CreateVisual();
        }

        private void CreateVisual()
        {
            if (visual == null)
            {
                visual = MaterialCache.CreatePrimitive(PrimitiveType.Cylinder);
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;

                visualRenderer = visual.GetComponent<Renderer>();
                if (visualRenderer != null)
                {
                    visualRenderer.material = MaterialCache.CreateUnlit(new Color(1f, 0.4f, 0.1f, 0.7f));
                }
            }

            visual.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
        }

        private void Update()
        {
            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                ReturnToPool();
                return;
            }

            // Fade visual as it expires
            if (visualRenderer != null && propBlock != null)
            {
                float alpha = Mathf.Clamp01(timer / duration);
                propBlock.SetColor("_Color", new Color(1f, 0.4f, 0.1f, 0.7f * alpha));
                visualRenderer.SetPropertyBlock(propBlock);
            }

            // Damage enemies in radius
            var mgr = EnemyManager.Instance;
            if (mgr == null) return;

            float dmgThisFrame = damagePerSecond * Time.deltaTime;
            var enemies = mgr.ActiveEnemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.IsDead) continue;
                if (enemy.IsFlying) continue;

                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist <= radius)
                {
                    enemy.TakeDamage(dmgThisFrame);
                    enemy.ApplyBurn(damagePerSecond, burnDuration);
                }
            }
        }

        private void OnDestroy()
        {
            if (propBlock != null)
            {
                MaterialCache.ReturnPropertyBlock(propBlock);
                propBlock = null;
            }
        }
    }
}
