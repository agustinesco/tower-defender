using UnityEngine;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class ResourcePopup : MonoBehaviour
    {
        private float duration = 2f;
        private float timer;
        private Vector3 startPos;
        private Vector3 endPos;
        private TextMesh textMesh;
        private Color textColor;

        public void Initialize(int amount, ResourceType resourceType, Vector3 from, Vector3 to)
        {
            startPos = from;
            endPos = to;
            transform.position = startPos;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = Vector3.zero;

            textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = $"+{amount} {resourceType}";
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.15f;
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

            // Lerp from mine to castle
            transform.position = Vector3.Lerp(startPos, endPos, t);

            // Fade out in the last 30% of travel
            float fadeStart = 0.7f;
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
