using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    public class QuestCardUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image questImage;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private Button cardButton;
        [SerializeField] private GameObject selectedImage;

        public Image Background => background;
        public Image QuestImage => questImage;
        public TextMeshProUGUI NameLabel => nameLabel;
        public Button CardButton => cardButton;
        public GameObject SelectedImage => selectedImage;
    }
}
