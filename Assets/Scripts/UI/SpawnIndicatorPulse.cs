using UnityEngine;

namespace TowerDefense.UI
{
    public class SpawnIndicatorPulse : MonoBehaviour
    {
        private Vector3 baseScale;

        private void Start()
        {
            baseScale = transform.localScale;
        }

        private void Update()
        {
            float pulse = 0.8f + 0.4f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2f));
            transform.localScale = baseScale * pulse;
        }
    }
}
