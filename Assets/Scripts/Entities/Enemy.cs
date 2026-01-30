using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Core;

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
        private float currentSpeed;
        private float speedMultiplier = 1f;
        private float slowTimer;

        public float Health => currentHealth;
        public float MaxHealth => baseHealth;
        public bool IsDead => currentHealth <= 0;

        public event System.Action<Enemy> OnDeath;
        public event System.Action<Enemy> OnReachedCastle;

        public void Initialize(List<Vector3> path, int waveNumber)
        {
            waypoints = path;
            currentWaypointIndex = 0;
            currentHealth = baseHealth + (waveNumber - 1) * 5f;
            currentSpeed = baseSpeed + (waveNumber - 1) * 0.1f;
            speedMultiplier = 1f;

            if (waypoints.Count > 0)
            {
                transform.position = waypoints[0];
            }

            CreateVisual();
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

        private void Update()
        {
            if (IsDead || waypoints == null || waypoints.Count == 0)
                return;

            UpdateSlowEffect();
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

        private void Die()
        {
            GameManager.Instance?.AddCurrency(currencyReward);
            OnDeath?.Invoke(this);
            Destroy(gameObject);
        }

        private void ReachCastle()
        {
            GameManager.Instance?.LoseLife();
            OnReachedCastle?.Invoke(this);
            Destroy(gameObject);
        }
    }
}