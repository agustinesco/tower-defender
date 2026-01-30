using UnityEngine;
using System.Collections.Generic;

namespace TowerDefense.Entities
{
    public class Tower : MonoBehaviour
    {
        private TowerData data;
        private float fireCooldown;
        private Enemy currentTarget;
        private GameObject rangeIndicator;
        private Transform turretHead;

        public TowerData Data => data;
        public int SellValue => data != null ? data.cost / 2 : 0;

        public void Initialize(TowerData towerData)
        {
            data = towerData;
            fireCooldown = 0f;
            CreateVisual();
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

        private void Update()
        {
            if (data == null) return;

            fireCooldown -= Time.deltaTime;

            FindTarget();

            if (currentTarget != null)
            {
                RotateTowardsTarget();

                if (fireCooldown <= 0f)
                {
                    Fire();
                    fireCooldown = 1f / data.fireRate;
                }
            }
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

            if (data.appliesSlow)
            {
                // Slow tower - instant effect
                currentTarget.ApplySlow(data.slowMultiplier, data.slowDuration);
            }
            else if (data.isAreaDamage)
            {
                // Area damage
                var enemies = FindObjectsOfType<Enemy>();
                foreach (var enemy in enemies)
                {
                    float distance = Vector3.Distance(currentTarget.transform.position, enemy.transform.position);
                    if (distance <= data.areaRadius)
                    {
                        enemy.TakeDamage(data.damage);
                    }
                }
            }
            else
            {
                // Single target damage
                currentTarget.TakeDamage(data.damage);
            }

            // Visual feedback - simple line
            Debug.DrawLine(turretHead.position, currentTarget.transform.position, Color.yellow, 0.1f);
        }
    }
}