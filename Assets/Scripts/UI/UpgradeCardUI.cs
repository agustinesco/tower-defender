using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    public class UpgradeCardUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text descriptionLabel;
        [SerializeField] private Text costLabel;
        [SerializeField] private Image buyButtonBg;
        [SerializeField] private Button buyButton;
        [SerializeField] private Text buyButtonText;

        public Image Background => background;
        public Text NameLabel => nameLabel;
        public Text DescriptionLabel => descriptionLabel;
        public Text CostLabel => costLabel;
        public Image BuyButtonBg => buyButtonBg;
        public Button BuyButton => buyButton;
        public Text BuyButtonText => buyButtonText;
    }
}
