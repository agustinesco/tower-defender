using UnityEngine;
using System.Collections.Generic;

namespace TowerDefense.UI
{
    public class CurrencyPopup : MonoBehaviour
    {
        private static readonly Queue<CurrencyPopup> pool = new Queue<CurrencyPopup>();

        private float lifetime = 1.2f;
        private float riseSpeed = 3f;
        private float timer;
        private TextMesh textMesh;
        private SpriteRenderer iconRenderer;
        private Color textColor;
        private Color iconColor;
        private bool initialized;

        public static CurrencyPopup GetFromPool(Vector3 position, int amount, Sprite goldSprite)
        {
            CurrencyPopup p = null;
            while (pool.Count > 0)
            {
                p = pool.Dequeue();
                if (p != null) break;
                p = null;
            }

            if (p == null)
            {
                var obj = new GameObject("CurrencyPopup");
                p = obj.AddComponent<CurrencyPopup>();
            }

            p.transform.position = position;
            p.gameObject.SetActive(true);
            p.Initialize(amount, goldSprite);
            return p;
        }

        private void ReturnToPool()
        {
            gameObject.SetActive(false);
            pool.Enqueue(this);
        }

        public void Initialize(int amount, Sprite goldSprite)
        {
            timer = 0f;
            textColor = new Color(1f, 0.9f, 0.2f);

            if (!initialized)
            {
                initialized = true;

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(transform);
                textObj.transform.localPosition = Vector3.zero;

                textMesh = textObj.AddComponent<TextMesh>();
                textMesh.fontSize = 48;
                textMesh.characterSize = 0.15f;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.fontStyle = FontStyle.Bold;

                var textRenderer = textObj.GetComponent<MeshRenderer>();
                textRenderer.sortingOrder = 100;

                if (goldSprite != null)
                {
                    var iconObj = new GameObject("GoldIcon");
                    iconObj.transform.SetParent(transform);
                    iconObj.transform.localPosition = new Vector3(1.2f, 0f, 0f);
                    iconObj.transform.localScale = Vector3.one * 0.7f;

                    iconRenderer = iconObj.AddComponent<SpriteRenderer>();
                    iconRenderer.sprite = goldSprite;
                    iconRenderer.sortingOrder = 100;
                }

                gameObject.AddComponent<BillboardSprite>();
            }

            textMesh.text = $"+{amount}";
            textMesh.color = textColor;

            if (iconRenderer != null)
            {
                iconColor = new Color(1f, 0.85f, 0.2f);
                iconRenderer.color = iconColor;
            }
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
                ReturnToPool();
        }
    }

    public class CastleDamagePopup : MonoBehaviour
    {
        private static readonly Queue<CastleDamagePopup> pool = new Queue<CastleDamagePopup>();

        private float lifetime = 1f;
        private float riseSpeed = 4f;
        private float timer;
        private TextMesh textMesh;
        private Color textColor;
        private bool initialized;

        public static CastleDamagePopup GetFromPool(Vector3 position)
        {
            CastleDamagePopup p = null;
            while (pool.Count > 0)
            {
                p = pool.Dequeue();
                if (p != null) break;
                p = null;
            }

            if (p == null)
            {
                var obj = new GameObject("CastleDamagePopup");
                p = obj.AddComponent<CastleDamagePopup>();
            }

            p.transform.position = position;
            p.gameObject.SetActive(true);
            p.Initialize();
            return p;
        }

        private void ReturnToPool()
        {
            gameObject.SetActive(false);
            pool.Enqueue(this);
        }

        public void Initialize()
        {
            timer = 0f;
            textColor = new Color(1f, 0.15f, 0.15f);

            if (!initialized)
            {
                initialized = true;

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(transform);
                textObj.transform.localPosition = Vector3.zero;

                textMesh = textObj.AddComponent<TextMesh>();
                textMesh.text = "-1";
                textMesh.fontSize = 64;
                textMesh.characterSize = 0.2f;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.fontStyle = FontStyle.Bold;

                var textRenderer = textObj.GetComponent<MeshRenderer>();
                textRenderer.sortingOrder = 100;

                gameObject.AddComponent<BillboardSprite>();
            }

            textMesh.color = textColor;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            transform.position += Vector3.up * riseSpeed * Time.deltaTime;

            float fadeStart = lifetime * 0.3f;
            if (timer > fadeStart)
            {
                float alpha = 1f - (timer - fadeStart) / (lifetime - fadeStart);
                textColor.a = alpha;
                if (textMesh != null)
                    textMesh.color = textColor;
            }

            if (timer >= lifetime)
                ReturnToPool();
        }
    }
}
