using UnityEngine;

namespace TowerDefense.UI
{
    public class CurrencyPopup : MonoBehaviour
    {
        private float lifetime = 1.2f;
        private float riseSpeed = 3f;
        private float timer;
        private TextMesh textMesh;
        private SpriteRenderer iconRenderer;
        private Color textColor;
        private Color iconColor;

        public void Initialize(int amount, Sprite goldSprite)
        {
            // Text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = Vector3.zero;

            textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = $"+{amount}";
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.15f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontStyle = FontStyle.Bold;
            textColor = new Color(1f, 0.9f, 0.2f);
            textMesh.color = textColor;

            var textRenderer = textObj.GetComponent<MeshRenderer>();
            textRenderer.sortingOrder = 100;

            // Gold icon (to the right of text)
            if (goldSprite != null)
            {
                var iconObj = new GameObject("GoldIcon");
                iconObj.transform.SetParent(transform);
                iconObj.transform.localPosition = new Vector3(1.2f, 0f, 0f);
                iconObj.transform.localScale = Vector3.one * 0.7f;

                iconRenderer = iconObj.AddComponent<SpriteRenderer>();
                iconRenderer.sprite = goldSprite;
                iconColor = new Color(1f, 0.85f, 0.2f);
                iconRenderer.color = iconColor;
                iconRenderer.sortingOrder = 100;
            }

            // Billboard
            gameObject.AddComponent<BillboardSprite>();
        }

        private void Update()
        {
            timer += Time.deltaTime;

            // Rise
            transform.position += Vector3.up * riseSpeed * Time.deltaTime;

            // Fade out in second half of lifetime
            float fadeStart = lifetime * 0.4f;
            if (timer > fadeStart)
            {
                float alpha = 1f - (timer - fadeStart) / (lifetime - fadeStart);

                textColor.a = alpha;
                if (textMesh != null)
                    textMesh.color = textColor;

                if (iconRenderer != null)
                {
                    iconColor.a = alpha;
                    iconRenderer.color = iconColor;
                }
            }

            if (timer >= lifetime)
                Destroy(gameObject);
        }
    }
}
