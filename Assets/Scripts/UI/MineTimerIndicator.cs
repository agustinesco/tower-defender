using UnityEngine;

namespace TowerDefense.UI
{
    public class MineTimerIndicator : MonoBehaviour
    {
        private float interval;
        private float timer;
        private LineRenderer ring;
        private TextMesh timerText;
        private const int Segments = 36;

        public void Initialize(float collectionInterval)
        {
            interval = collectionInterval;
            timer = 0f;

            // Progress ring
            var ringObj = new GameObject("TimerRing");
            ringObj.transform.SetParent(transform);
            ringObj.transform.localPosition = Vector3.zero;

            ring = ringObj.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = false;
            ring.startWidth = 0.3f;
            ring.endWidth = 0.3f;
            ring.positionCount = Segments + 1;
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = new Color(0.2f, 0.8f, 1f);
            ring.material = mat;

            // Countdown text
            var textObj = new GameObject("TimerText");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = Vector3.up * 0.5f;

            timerText = textObj.AddComponent<TextMesh>();
            timerText.fontSize = 48;
            timerText.characterSize = 0.08f;
            timerText.anchor = TextAnchor.MiddleCenter;
            timerText.alignment = TextAlignment.Center;
            timerText.color = new Color(0.2f, 0.8f, 1f);

            textObj.AddComponent<BillboardSprite>();

            UpdateRing(0f);
        }

        public void SetTimer(float currentTimer)
        {
            timer = currentTimer;
        }

        private void Update()
        {
            float remaining = Mathf.Max(0f, interval - timer);
            int seconds = Mathf.CeilToInt(remaining);
            if (timerText != null)
                timerText.text = $"{seconds}s";

            float progress = Mathf.Clamp01(timer / interval);
            UpdateRing(progress);

            timer += Time.deltaTime;
        }

        private void UpdateRing(float progress)
        {
            if (ring == null) return;

            int filledSegments = Mathf.RoundToInt(progress * Segments);
            ring.positionCount = filledSegments + 1;

            float radius = 1.8f;
            for (int i = 0; i <= filledSegments; i++)
            {
                float angle = (i / (float)Segments) * 360f * Mathf.Deg2Rad;
                // Start from top (90 degrees), go clockwise
                float x = Mathf.Cos(Mathf.PI / 2f - angle) * radius;
                float z = Mathf.Sin(Mathf.PI / 2f - angle) * radius;
                ring.SetPosition(i, new Vector3(x, 0f, z));
            }
        }
    }
}
