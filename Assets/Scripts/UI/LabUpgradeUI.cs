using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    public class LabUpgradeUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image accent;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;
        [SerializeField] private TextMeshProUGUI levelLabel;
        [SerializeField] private Image buyButtonBg;
        [SerializeField] private Button buyButton;
        [SerializeField] private TextMeshProUGUI buyButtonText;

        public Image Background => background;
        public Image Accent => accent;
        public TextMeshProUGUI NameLabel => nameLabel;
        public TextMeshProUGUI DescriptionLabel => descriptionLabel;
        public TextMeshProUGUI LevelLabel => levelLabel;
        public Image BuyButtonBg => buyButtonBg;
        public Button BuyButton => buyButton;
        public TextMeshProUGUI BuyButtonText => buyButtonText;
    }
}
