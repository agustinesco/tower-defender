using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    public class UpgradeCardUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;
        [SerializeField] private TextMeshProUGUI costLabel;
        [SerializeField] private Image buyButtonBg;
        [SerializeField] private Button buyButton;
        [SerializeField] private TextMeshProUGUI buyButtonText;

        public Image Background => background;
        public TextMeshProUGUI NameLabel => nameLabel;
        public TextMeshProUGUI DescriptionLabel => descriptionLabel;
        public TextMeshProUGUI CostLabel => costLabel;
        public Image BuyButtonBg => buyButtonBg;
        public Button BuyButton => buyButton;
        public TextMeshProUGUI BuyButtonText => buyButtonText;
    }
}
