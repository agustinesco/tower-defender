using UnityEngine;

namespace TowerDefense.Entities
{
    public class FirePatch : MonoBehaviour
    {
        private float damagePerSecond;
        private float duration;
        private float burnDuration;
        private float radius;
        private float timer;
        private GameObject visual;

        public void Initialize(float dps, float duration, float burnDuration, float radius)
        {
            this.damagePerSecond = dps;
            this.duration = duration;
            this.burnDuration = burnDuration;
            this.radius = radius;
            this.timer = duration;

            CreateVisual();
        }

        private void CreateVisual()
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);

            var collider = visual.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Unlit/Color"));
                renderer.material.color = new Color(1f, 0.4f, 0.1f, 0.7f);
            }
        }

        private void Update()
        {
            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Fade visual as it expires
            if (visual != null)
            {
                float alpha = Mathf.Clamp01(timer / duration);
                var renderer = visual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(1f, 0.4f, 0.1f, 0.7f * alpha);
                }
            }

            // Damage enemies in radius
            var enemies = FindObjectsOfType<Enemy>();
            float dmgThisFrame = damagePerSecond * Time.deltaTime;

            foreach (var enemy in enemies)
            {
                if (enemy.IsDead) continue;

                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist <= radius)
                {
                    enemy.TakeDamage(dmgThisFrame);
                    enemy.ApplyBurn(damagePerSecond, burnDuration);
                }
            }
        }
    }
}
