using UnityEngine;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class ResourcePopup : MonoBehaviour
    {
        private float duration = 2.5f;
        private float timer;
        private Vector3 startPos;
        private Vector3 endPos;
        private Vector3 midPos;
        private TextMesh textMesh;
        private Color textColor;

        public void Initialize(int amount, ResourceType resourceType, Vector3 from, Vector3 to)
        {
            startPos = from;
            endPos = to;
            midPos = Vector3.Lerp(startPos, endPos, 0.5f) + Vector3.up * 8f;
            transform.position = startPos;
            transform.localScale = Vector3.one * 2f;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = Vector3.zero;

            textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = $"+{amount} {resourceType}";
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.3f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontStyle = FontStyle.Bold;

            textColor = Core.GameManager.Instance.GetResourceColor(resourceType);
            textMesh.color = textColor;

            var textRenderer = textObj.GetComponent<MeshRenderer>();
            textRenderer.sortingOrder = 100;

            gameObject.AddComponent<BillboardSprite>();
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            // Scale burst: start at 2x, settle to 1x over first 0.4s
            if (timer < 0.4f)
            {
                float scaleT = timer / 0.4f;
                float scale = Mathf.Lerp(2f, 1f, scaleT);
                transform.localScale = Vector3.one * scale;
            }
            else
            {
                transform.localScale = Vector3.one;
            }

            // Quadratic bezier arc (parabolic path)
            float oneMinusT = 1f - t;
            transform.position = oneMinusT * oneMinusT * startPos
                + 2f * oneMinusT * t * midPos
                + t * t * endPos;

            // Fade out in the last 40% of travel
            float fadeStart = 0.6f;
            if (t > fadeStart)
            {
                float alpha = 1f - (t - fadeStart) / (1f - fadeStart);
                textColor.a = alpha;
                if (textMesh != null)
                    textMesh.color = textColor;
            }

            if (timer >= duration)
                Destroy(gameObject);
        }
    }
}
