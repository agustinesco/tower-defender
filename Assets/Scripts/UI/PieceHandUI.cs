using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TowerDefense.Grid;
using TowerDefense.Data;
using TowerDefense.Core;
using TowerDefense.Entities;

namespace TowerDefense.UI
{
    public enum ModificationType { Mine, Lure, Haste, GoldenTouch }

    public class PieceHandUI : MonoBehaviour
    {
        private enum HandTab { Paths, Towers, Modifications }

        [Header("Scene References")]
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private VerticalLayoutGroup cardContainerLayout;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Button pathsTabButton;
        [SerializeField] private Button towersTabButton;
        [SerializeField] private Button modsTabButton;
        [SerializeField] private Image pathsTabImage;
        [SerializeField] private Image towersTabImage;
        [SerializeField] private Image modsTabImage;
        [SerializeField] private GameObject towersTabObj;
        [SerializeField] private GameObject modsTabObj;
        [SerializeField] private GameObject tooltipObj;
        [SerializeField] private Text tooltipText;

        private List<PieceCard> cards = new List<PieceCard>();
        private int selectedIndex = -1;
        private Dictionary<HexPieceType, HexPieceConfig> pieceConfigs;
        private PieceProvider pieceProvider;

        private HandTab activeTab = HandTab.Paths;

        private bool freeTowerMode;
        private List<TowerData> availableTowers;

        private static readonly Color TabActiveColor = new Color(0.3f, 0.6f, 0.3f);
        private static readonly Color TabInactiveColor = new Color(0.25f, 0.25f, 0.25f);

        private const float BaseCardSpacing = 12f;
        private const float PanelPadding = 15f;

        private float cardSpacing = BaseCardSpacing;
        private float fontScale = 1f;

        private static readonly Dictionary<ModificationType, string> ModDescriptions = new Dictionary<ModificationType, string>
        {
            { ModificationType.Lure, "Places a one-time lure that spawns extra enemies next wave. Lured enemies give 2x gold" },
            { ModificationType.Haste, "Increases the fire rate of all towers on this tile by 30% for the next wave (or 2 minutes)" },
            { ModificationType.GoldenTouch, "Enemies killed on this tile drop 1.5x gold for the next wave (or 2 minutes)" }
        };

        public event Action<int, HexPieceType> OnCardSelected;
        public event Action OnCardDeselected;
        public event Action<ModificationType> OnModificationSelected;
        public event Action OnModificationDeselected;
        public event Action<TowerData> OnTowerCardSelected;
        public event Action OnTowerCardDeselected;
        public event Action<int> OnTabSwitched;

        private class PieceCard
        {
            public CardUI UI;
            public int HandIndex;
            public HexPieceType PieceType;
            public List<GameObject> PathPreviewLines;
            public bool IsModificationCard;
            public ModificationType ModType;
            public bool IsTowerCard;
            public TowerData TowerData;
            public bool IsLocked;
        }

        private CanvasScaler parentCanvasScaler;

        private void Awake()
        {
            parentCanvasScaler = GetComponentInParent<CanvasScaler>();
        }

        private void Start()
        {
            if (pathsTabButton != null)
                pathsTabButton.onClick.AddListener(() => SwitchTab(HandTab.Paths));
            if (towersTabButton != null)
                towersTabButton.onClick.AddListener(() => SwitchTab(HandTab.Towers));
            if (modsTabButton != null)
                modsTabButton.onClick.AddListener(() => SwitchTab(HandTab.Modifications));
        }

        private float GetEffectiveCanvasHeight()
        {
            if (parentCanvasScaler == null) return 720f;
            var refRes = parentCanvasScaler.referenceResolution;
            float screenW = Screen.width;
            float screenH = Screen.height;
            if (screenW <= 0 || screenH <= 0) return refRes.y;

            float match = parentCanvasScaler.matchWidthOrHeight;
            float logW = Mathf.Log(screenW / refRes.x, 2f);
            float logH = Mathf.Log(screenH / refRes.y, 2f);
            float logScale = Mathf.Lerp(logW, logH, match);
            float scale = Mathf.Pow(2f, logScale);
            return screenH / scale;
        }

        public void SetPieceConfigs(Dictionary<HexPieceType, HexPieceConfig> configs)
        {
            pieceConfigs = configs;
        }

        public void SetPieceProvider(PieceProvider provider)
        {
            pieceProvider = provider;
        }

        public void SetFreeTowerMode(bool enabled)
        {
            freeTowerMode = enabled;
        }

        public void SetAvailableTowers(List<TowerData> towers)
        {
            availableTowers = towers;
        }

        public void DeselectTowerCard()
        {
            if (activeTab == HandTab.Towers && selectedIndex >= 0)
            {
                selectedIndex = -1;
                UpdateBorders();
                HideTooltip();
                OnTowerCardDeselected?.Invoke();
            }
        }

        private int GetMaxCardCount()
        {
            int pathCount = pieceProvider != null ? pieceProvider.Pieces.Count : 0;
            int towerCount = availableTowers != null ? availableTowers.Count : 0;
            int modCount = 3;
            return Mathf.Max(pathCount, Mathf.Max(towerCount, modCount));
        }

        private void ComputeCardSizing()
        {
            int maxCards = GetMaxCardCount();
            if (maxCards <= 0) return;

            cardSpacing = BaseCardSpacing;

            if (cardContainerLayout != null)
                cardContainerLayout.spacing = cardSpacing;
        }

        private bool HasUnlockedTowers()
        {
            if (availableTowers == null) return false;
            for (int i = 0; i < availableTowers.Count; i++)
            {
                if (LabManager.Instance == null || LabManager.Instance.IsTowerUnlocked(availableTowers[i].towerName))
                    return true;
            }
            return false;
        }

        private bool HasUnlockedMods()
        {
            if (LabManager.Instance == null) return true;
            if (LabManager.Instance.IsModUnlocked("Lure")) return true;
            if (LabManager.Instance.IsModUnlocked("Haste")) return true;
            if (LabManager.Instance.IsModUnlocked("GoldenTouch")) return true;
            return false;
        }

        private void UpdateTabVisibility()
        {
            bool showTowers = freeTowerMode && HasUnlockedTowers();
            bool showMods = HasUnlockedMods();

            if (towersTabObj != null)
                towersTabObj.SetActive(showTowers);
            if (modsTabObj != null)
                modsTabObj.SetActive(showMods);

            if (activeTab == HandTab.Towers && !showTowers)
                activeTab = HandTab.Paths;
            if (activeTab == HandTab.Modifications && !showMods)
                activeTab = HandTab.Paths;

            UpdateTabColors();
        }

        private void UpdateTabColors()
        {
            if (pathsTabImage != null)
                pathsTabImage.color = activeTab == HandTab.Paths ? TabActiveColor : TabInactiveColor;
            if (towersTabImage != null)
                towersTabImage.color = activeTab == HandTab.Towers ? TabActiveColor : TabInactiveColor;
            if (modsTabImage != null)
                modsTabImage.color = activeTab == HandTab.Modifications ? TabActiveColor : TabInactiveColor;
        }

        private void SwitchTab(HandTab tab)
        {
            if (activeTab == tab) return;

            // Tutorial gate
            var tut = TowerDefense.Core.TutorialManager.Instance;
            if (tut != null && !tut.AllowTabSwitch((int)tab))
                return;

            if (activeTab == HandTab.Paths && selectedIndex >= 0)
            {
                selectedIndex = -1;
                OnCardDeselected?.Invoke();
            }
            else if (activeTab == HandTab.Towers)
            {
                selectedIndex = -1;
                HideTooltip();
                OnTowerCardDeselected?.Invoke();
            }
            else if (activeTab == HandTab.Modifications)
            {
                selectedIndex = -1;
                HideTooltip();
                OnModificationDeselected?.Invoke();
            }

            activeTab = tab;
            UpdateTabColors();

            if (tab == HandTab.Paths)
            {
                ClearCards();
                if (pieceProvider != null)
                    RefreshHand(pieceProvider.Pieces);
            }
            else if (tab == HandTab.Towers)
            {
                RefreshTowers();
            }
            else
            {
                RefreshModifications();
            }

            OnTabSwitched?.Invoke((int)tab);
        }

        private void ClearCards()
        {
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                if (cards[i].UI != null)
                    Destroy(cards[i].UI.gameObject);
            }
            cards.Clear();
        }

        public void RefreshHand(IReadOnlyList<HexPieceConfig> pieces)
        {
            if (activeTab != HandTab.Paths) return;

            if (cards.Count != pieces.Count)
            {
                ClearCards();
                ComputeCardSizing();
                UpdateTabVisibility();

                for (int i = 0; i < pieces.Count; i++)
                {
                    InstantiatePathCard(i, pieces[i]);
                }
            }

            if (selectedIndex >= pieces.Count)
            {
                selectedIndex = -1;
                OnCardDeselected?.Invoke();
            }

            UpdateBorders();
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);
        }

        private void InstantiatePathCard(int index, HexPieceConfig config)
        {
            var go = Instantiate(cardPrefab, cardContainer);
            var ui = go.GetComponent<CardUI>();
            ui.Background.color = config.cardColor;
            ui.CostLabel.text = $"{config.placementCost}";
            ui.Icon.gameObject.SetActive(false);

            int capturedIndex = index;
            HexPieceType capturedType = config.pieceType;
            ui.Button.onClick.AddListener(() => OnCardClicked(capturedIndex, capturedType));

            CreatePathPreview(ui.PathPreviewContainer, config);

            // Cooldown text font size
            if (ui.CooldownText != null)
                ui.CooldownText.fontSize = Mathf.RoundToInt(24 * fontScale);

            var card = new PieceCard
            {
                UI = ui,
                HandIndex = index,
                PieceType = config.pieceType,
                PathPreviewLines = new List<GameObject>(),
                IsModificationCard = false
            };
            cards.Add(card);
        }

        public void RefreshTowers()
        {
            if (activeTab != HandTab.Towers) return;

            ClearCards();
            selectedIndex = -1;

            if (availableTowers == null || availableTowers.Count == 0) return;

            var unlockedTowers = new List<TowerData>();
            for (int i = 0; i < availableTowers.Count; i++)
            {
                if (LabManager.Instance == null || LabManager.Instance.IsTowerUnlocked(availableTowers[i].towerName))
                    unlockedTowers.Add(availableTowers[i]);
            }

            if (unlockedTowers.Count == 0) return;

            ComputeCardSizing();

            for (int i = 0; i < unlockedTowers.Count; i++)
            {
                InstantiateTowerCard(i, unlockedTowers[i]);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);
        }

        private void InstantiateTowerCard(int index, TowerData towerData)
        {
            var go = Instantiate(cardPrefab, cardContainer);
            var ui = go.GetComponent<CardUI>();
            ui.Background.color = towerData.towerColor;
            ui.CostLabel.text = $"{towerData.cost}";

            // Icon
            if (towerData.towerIcon != null)
            {
                ui.Icon.sprite = towerData.towerIcon;
                ui.Icon.color = Color.white;
                ui.Icon.preserveAspect = true;
                ui.Icon.gameObject.SetActive(true);
            }
            else
            {
                ui.Icon.color = towerData.towerColor * 0.7f;
                ui.Icon.gameObject.SetActive(true);
            }

            // Hide path preview container (not used for towers)
            if (ui.PathPreviewContainer != null)
                ui.PathPreviewContainer.gameObject.SetActive(false);

            // Hide cooldown overlay (towers don't have cooldowns)
            ui.SetCooldownActive(false);

            int capturedIndex = index;
            TowerData capturedData = towerData;
            ui.Button.onClick.AddListener(() => OnTowerCardClicked(capturedIndex, capturedData));

            var card = new PieceCard
            {
                UI = ui,
                HandIndex = index,
                PathPreviewLines = new List<GameObject>(),
                IsModificationCard = false,
                IsTowerCard = true,
                TowerData = towerData,
                IsLocked = false
            };
            cards.Add(card);
        }

        private void OnTowerCardClicked(int index, TowerData towerData)
        {
            // Tutorial gate
            var tut = TowerDefense.Core.TutorialManager.Instance;
            if (tut != null && !tut.AllowCardSelect(index))
                return;

            if (selectedIndex == index)
            {
                selectedIndex = -1;
                UpdateBorders();
                HideTooltip();
                OnTowerCardDeselected?.Invoke();
            }
            else
            {
                selectedIndex = index;
                UpdateBorders();
                HideTooltip();
                OnTowerCardSelected?.Invoke(towerData);
            }
        }

        private void RefreshModifications()
        {
            ClearCards();
            selectedIndex = -1;

            var gm = GameManager.Instance;
            int lureCost = gm != null ? gm.GetLureCost() : 0;
            int hasteCost = gm != null ? gm.GetHasteCost() : 0;
            int goldenTouchCost = gm != null ? gm.GetGoldenTouchCost() : 0;

            var modEntries = new List<(ModificationType type, string label, int cost, Color color)>();
            if (LabManager.Instance == null || LabManager.Instance.IsModUnlocked("Lure"))
                modEntries.Add((ModificationType.Lure, "Lure", lureCost, new Color(0.8f, 0.7f, 0.2f)));
            if (LabManager.Instance == null || LabManager.Instance.IsModUnlocked("Haste"))
                modEntries.Add((ModificationType.Haste, "Haste", hasteCost, new Color(0.9f, 0.4f, 0.1f)));
            if (LabManager.Instance == null || LabManager.Instance.IsModUnlocked("GoldenTouch"))
                modEntries.Add((ModificationType.GoldenTouch, "Golden", goldenTouchCost, new Color(0.9f, 0.75f, 0.1f)));

            if (modEntries.Count == 0) return;

            ComputeCardSizing();

            for (int i = 0; i < modEntries.Count; i++)
            {
                var (type, label, cost, color) = modEntries[i];
                InstantiateModCard(i, type, label, cost, color);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);
        }

        private void InstantiateModCard(int index, ModificationType modType, string label, int cost, Color color)
        {
            var go = Instantiate(cardPrefab, cardContainer);
            var ui = go.GetComponent<CardUI>();
            ui.Background.color = color;
            ui.CostLabel.text = $"{cost}";

            // Icon from Resources
            string spriteName = modType switch
            {
                ModificationType.Mine => "gold-mine",
                ModificationType.Lure => "fishing-lure",
                ModificationType.Haste => "speedometer",
                ModificationType.GoldenTouch => "gold-bar",
                _ => null
            };
            var sprite = spriteName != null ? Resources.Load<Sprite>($"ModIcons/{spriteName}") : null;
            if (sprite != null)
            {
                ui.Icon.sprite = sprite;
                ui.Icon.color = Color.white;
                ui.Icon.preserveAspect = true;
                ui.Icon.gameObject.SetActive(true);
            }
            else
            {
                ui.Icon.color = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f);
                ui.Icon.gameObject.SetActive(true);
            }

            // Hide path preview container
            if (ui.PathPreviewContainer != null)
                ui.PathPreviewContainer.gameObject.SetActive(false);

            // Hide cooldown overlay
            ui.SetCooldownActive(false);

            int capturedIndex = index;
            ModificationType capturedType = modType;
            ui.Button.onClick.AddListener(() => OnModificationCardClicked(capturedIndex, capturedType));

            var card = new PieceCard
            {
                UI = ui,
                HandIndex = index,
                PathPreviewLines = new List<GameObject>(),
                IsModificationCard = true,
                ModType = modType
            };
            cards.Add(card);
        }

        private void OnModificationCardClicked(int index, ModificationType type)
        {
            if (selectedIndex == index)
            {
                selectedIndex = -1;
                UpdateBorders();
                HideTooltip();
                OnModificationDeselected?.Invoke();
            }
            else
            {
                selectedIndex = index;
                UpdateBorders();
                ShowTooltip(type);
                OnModificationSelected?.Invoke(type);
            }
        }

        private void Update()
        {
            if (pieceProvider == null || activeTab != HandTab.Paths) return;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card.UI == null || card.UI.CooldownOverlay == null) continue;

                float fraction = pieceProvider.GetCooldownFraction(i);
                bool onCooldown = fraction > 0f;

                card.UI.SetCooldownActive(onCooldown);

                if (onCooldown)
                {
                    card.UI.SetCooldownFill(fraction);
                    float remaining = pieceProvider.GetCooldownRemaining(i);
                    card.UI.SetCooldownText(Mathf.CeilToInt(remaining).ToString());
                }
                else
                {
                    card.UI.SetCooldownText(null);
                }
            }
        }

        private void OnCardClicked(int index, HexPieceType type)
        {
            if (pieceProvider != null && !pieceProvider.IsReady(index))
                return;

            // Tutorial gate
            var tut = TowerDefense.Core.TutorialManager.Instance;
            if (tut != null && !tut.AllowCardSelect(index))
                return;

            if (selectedIndex == index)
            {
                selectedIndex = -1;
                UpdateBorders();
                OnCardDeselected?.Invoke();
            }
            else
            {
                selectedIndex = index;
                UpdateBorders();
                OnCardSelected?.Invoke(index, type);
            }
        }

        private void UpdateBorders()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card.UI == null) continue;

                bool isSelected = card.HandIndex == selectedIndex;
                card.UI.SetSelected(isSelected);
            }
        }

        public void Deselect()
        {
            selectedIndex = -1;
            UpdateBorders();
            HideTooltip();
        }

        private void ShowTooltip(ModificationType type)
        {
            if (tooltipObj == null) return;

            if (ModDescriptions.TryGetValue(type, out var desc))
                tooltipText.text = desc;

            tooltipObj.SetActive(true);
        }

        private void ShowTowerTooltip(TowerData data)
        {
            if (tooltipObj == null) return;

            string desc = $"{data.towerName} | DMG: {data.damage} | Rate: {data.fireRate}/s | Range: {data.range}";
            if (data.isShotgun) desc += " | Shotgun";
            if (data.isTesla) desc += " | Chain Lightning";
            if (data.isFlame) desc += " | Fire";
            if (data.appliesSlow) desc += " | Slow Aura";
            tooltipText.text = desc;

            tooltipObj.SetActive(true);
        }

        private void HideTooltip()
        {
            if (tooltipObj != null)
                tooltipObj.SetActive(false);
        }

        public RectTransform GetCardRectTransform(int index)
        {
            if (index >= 0 && index < cards.Count && cards[index].UI != null)
                return cards[index].UI.GetComponent<RectTransform>();
            return null;
        }

        public RectTransform GetTowersTabRectTransform()
        {
            if (towersTabButton != null)
                return towersTabButton.GetComponent<RectTransform>();
            return null;
        }

        private void CreatePathPreview(RectTransform container, HexPieceConfig config)
        {
            if (container == null) return;

            List<float> angles = config.previewAngles;

            foreach (float angle in angles)
            {
                GameObject arm = new GameObject("Arm");
                arm.transform.SetParent(container);
                var armRect = arm.AddComponent<RectTransform>();
                armRect.anchorMin = new Vector2(0.5f, 0.5f);
                armRect.anchorMax = new Vector2(0.5f, 0.5f);
                armRect.anchoredPosition = Vector2.zero;
                armRect.sizeDelta = new Vector2(6f, 22f);
                armRect.pivot = new Vector2(0.5f, 0f);
                armRect.localRotation = Quaternion.Euler(0f, 0f, angle);
                armRect.localScale = Vector3.one;

                var armImage = arm.AddComponent<Image>();
                armImage.color = new Color(0.35f, 0.25f, 0.15f);
            }
        }
    }
}
