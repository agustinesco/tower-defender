using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    public class CardUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private GameObject borderObj;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI costLabel;
        [SerializeField] private GameObject cooldownOverlay;
        [SerializeField] private Image cooldownImage;
        [SerializeField] private TextMeshProUGUI cooldownText;
        [SerializeField] private Button button;
        [SerializeField] private LayoutElement layoutElement;
        [SerializeField] private RectTransform pathPreviewContainer;

        public Image Background => background;
        public GameObject BorderObj => borderObj;
        public Image BorderImage => borderImage;
        public Image Icon => icon;
        public TextMeshProUGUI CostLabel => costLabel;
        public GameObject CooldownOverlay => cooldownOverlay;
        public Image CooldownImage => cooldownImage;
        public TextMeshProUGUI CooldownText => cooldownText;
        public Button Button => button;
        public LayoutElement LayoutElement => layoutElement;
        public RectTransform PathPreviewContainer => pathPreviewContainer;

        public void SetSelected(bool selected)
        {
            if (borderObj != null)
                borderObj.SetActive(selected);
        }

        public void SetCooldownActive(bool active)
        {
            if (cooldownOverlay != null)
                cooldownOverlay.SetActive(active);
        }

        public void SetCooldownFill(float fraction)
        {
            if (cooldownImage == null) return;
            var rect = cooldownImage.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f - fraction);
            rect.anchorMax = Vector2.one;
        }

        public void SetCooldownText(string text)
        {
            if (cooldownText != null)
                cooldownText.text = text;
            if (cooldownText != null)
                cooldownText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void SetAffordable(bool canAfford)
        {
            if (button != null)
                button.interactable = canAfford;
            if (background != null)
            {
                var c = background.color;
                background.color = new Color(c.r, c.g, c.b, canAfford ? 1f : 0.35f);
            }
            if (icon != null && icon.gameObject.activeSelf)
            {
                var c = icon.color;
                icon.color = new Color(c.r, c.g, c.b, canAfford ? 1f : 0.35f);
            }
            if (costLabel != null)
            {
                costLabel.color = canAfford ? Color.white : new Color(1f, 0.3f, 0.3f, 0.7f);
            }
        }
    }
}
