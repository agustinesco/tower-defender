using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    public class QuestCardUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI objectivesLabel;
        [SerializeField] private TextMeshProUGUI rewardLabel;
        [SerializeField] private Image actionButtonBg;
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionButtonText;

        public Image Background => background;
        public TextMeshProUGUI NameLabel => nameLabel;
        public TextMeshProUGUI ObjectivesLabel => objectivesLabel;
        public TextMeshProUGUI RewardLabel => rewardLabel;
        public Image ActionButtonBg => actionButtonBg;
        public Button ActionButton => actionButton;
        public TextMeshProUGUI ActionButtonText => actionButtonText;
    }
}