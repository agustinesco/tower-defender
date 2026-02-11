using UnityEngine;

namespace TowerDefense.UI
{
    /// <summary>
    /// Adjusts this RectTransform's anchors to match Screen.safeArea.
    /// Place as a full-stretch child of the Canvas; content parented under it
    /// will automatically respect device notches/cutouts.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        [SerializeField] private bool applyLeft = true;
        [SerializeField] private bool applyRight = true;
        [SerializeField] private bool applyTop = true;
        [SerializeField] private bool applyBottom = true;

        private RectTransform rectTransform;
        private int lastScreenW;
        private int lastScreenH;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            int sw = Screen.width;
            int sh = Screen.height;
            if (sw != lastScreenW || sh != lastScreenH)
            {
                Apply();
            }
        }

        private void Apply()
        {
#if UNITY_EDITOR
            // In the editor, keep the anchors baked in the prefab.
            return;
#endif
            var safeArea = Screen.safeArea;
            int screenW = Screen.width;
            int screenH = Screen.height;

            lastScreenW = screenW;
            lastScreenH = screenH;

            if (screenW <= 0 || screenH <= 0) return;

            float minX = applyLeft ? safeArea.x / screenW : 0f;
            float minY = applyBottom ? safeArea.y / screenH : 0f;
            float maxX = applyRight ? (safeArea.x + safeArea.width) / screenW : 1f;
            float maxY = applyTop ? (safeArea.y + safeArea.height) / screenH : 1f;

            rectTransform.anchorMin = new Vector2(minX, minY);
            rectTransform.anchorMax = new Vector2(maxX, maxY);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
