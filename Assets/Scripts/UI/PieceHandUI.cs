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

        private RectTransform panelRect;
        private List<PieceCard> cards = new List<PieceCard>();
        private int selectedIndex = -1;
        private Dictionary<HexPieceType, HexPieceConfig> pieceConfigs;
        private PieceProvider pieceProvider;

        private HandTab activeTab = HandTab.Paths;
        private GameObject pathsTabBtn;
        private GameObject towersTabBtn;
        private GameObject modsTabBtn;
        private Image pathsTabImage;
        private Image towersTabImage;
        private Image modsTabImage;

        private bool freeTowerMode;
        private List<TowerData> availableTowers;

        private static readonly Color TabActiveColor = new Color(0.3f, 0.6f, 0.3f);
        private static readonly Color TabInactiveColor = new Color(0.25f, 0.25f, 0.25f);

        private const float CardWidth = 90f;
        private const float CardHeight = 110f;
        private const float CardSpacing = 10f;
        private const float PanelPadding = 15f;
        private const float TabColumnWidth = 50f;

        private GameObject tooltipObj;
        private Text tooltipText;
        private static readonly Dictionary<ModificationType, string> ModDescriptions = new Dictionary<ModificationType, string>
        {
            { ModificationType.Mine, "Builds a mining outpost that periodically collects resources from nearby ore nodes" },
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

        private class PieceCard
        {
            public GameObject CardObject;
            public Image Background;
            public GameObject BorderObj;
            public Image BorderImage;
            public Text TypeLabel;
            public Text CostLabel;
            public GameObject CooldownOverlay;
            public Image CooldownImage;
            public Text CooldownText;
            public int HandIndex;
            public HexPieceType PieceType;
            public List<GameObject> PathPreviewLines;
            public bool IsModificationCard;
            public ModificationType ModType;
            public bool IsTowerCard;
            public TowerData TowerData;
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
            int modCount = 4;
            return Mathf.Max(pathCount, Mathf.Max(towerCount, modCount));
        }

        private void CreatePanel(int pieceCount)
        {
            if (panelRect != null)
            {
                Destroy(panelRect.gameObject);
                tooltipObj = null;
            }

            GameObject panel = new GameObject("PieceHandPanel");
            panel.transform.SetParent(transform);

            panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);

            int maxCards = GetMaxCardCount();
            float cardsWidth = maxCards * CardWidth + (maxCards - 1) * CardSpacing + PanelPadding * 2f;
            float totalWidth = cardsWidth + TabColumnWidth;
            float panelHeight = CardHeight + PanelPadding * 2f;
            panelRect.sizeDelta = new Vector2(totalWidth, panelHeight);
            panelRect.anchoredPosition = Vector2.zero;

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            // Create tab column on the left side
            CreateTabColumn(panel.transform, panelHeight);
        }

        private void CreateTabColumn(Transform parent, float panelHeight)
        {
            pathsTabBtn = null;
            towersTabBtn = null;
            modsTabBtn = null;

            int tabCount = freeTowerMode ? 3 : 2;
            float tabHeight = (panelHeight - (tabCount + 1) * 2f) / tabCount;

            // Paths tab button
            float pathsY = (tabCount == 3)
                ? tabHeight + 2f
                : tabHeight / 4f + 1f;

            pathsTabBtn = CreateTabButton(parent, "PathsTab", "Path", pathsY, tabHeight, HandTab.Paths);
            pathsTabImage = pathsTabBtn.GetComponent<Image>();

            // Towers tab button (only in free mode)
            if (freeTowerMode)
            {
                towersTabBtn = CreateTabButton(parent, "TowersTab", "Tower", 0f, tabHeight, HandTab.Towers);
                towersTabImage = towersTabBtn.GetComponent<Image>();
            }

            // Mods tab button
            float modsY = (tabCount == 3)
                ? -(tabHeight + 2f)
                : -(tabHeight / 4f + 1f);

            modsTabBtn = CreateTabButton(parent, "ModsTab", "Mods", modsY, tabHeight, HandTab.Modifications);
            modsTabImage = modsTabBtn.GetComponent<Image>();

            UpdateTabColors();
        }

        private GameObject CreateTabButton(Transform parent, string name, string label, float yPos, float height, HandTab tab)
        {
            var btn = new GameObject(name);
            btn.transform.SetParent(parent);
            var rect = btn.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(TabColumnWidth / 2f + 2f, yPos);
            rect.sizeDelta = new Vector2(TabColumnWidth - 4f, height - 2f);

            var image = btn.AddComponent<Image>();
            var button = btn.AddComponent<Button>();
            button.targetGraphic = image;
            HandTab capturedTab = tab;
            button.onClick.AddListener(() => SwitchTab(capturedTab));

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btn.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 11;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            return btn;
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

            // Deselect any current selection and clear highlights
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
                foreach (var card in cards)
                {
                    if (card.CardObject != null)
                        Destroy(card.CardObject);
                }
                cards.Clear();

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
        }

        public void RefreshHand(IReadOnlyList<HexPieceConfig> pieces)
        {
            if (activeTab != HandTab.Paths) return;

            if (cards.Count != pieces.Count)
            {
                foreach (var card in cards)
                {
                    if (card.CardObject != null)
                        Destroy(card.CardObject);
                }
                cards.Clear();

                CreatePanel(pieces.Count);

                if (panelRect == null) return;

                float startX = -(pieces.Count - 1) * (CardWidth + CardSpacing) / 2f + TabColumnWidth / 2f;

                for (int i = 0; i < pieces.Count; i++)
                {
                    CreateCard(i, pieces[i], startX + i * (CardWidth + CardSpacing));
                }
            }

            if (selectedIndex >= pieces.Count)
            {
                selectedIndex = -1;
                OnCardDeselected?.Invoke();
            }

            UpdateBorders();
        }

        private void RefreshTowers()
        {
            foreach (var card in cards)
            {
                if (card.CardObject != null)
                    Destroy(card.CardObject);
            }
            cards.Clear();
            selectedIndex = -1;

            if (availableTowers == null || availableTowers.Count == 0) return;

            int towerCount = availableTowers.Count;
            CreatePanel(towerCount);

            if (panelRect == null) return;

            float startX = -(towerCount - 1) * (CardWidth + CardSpacing) / 2f + TabColumnWidth / 2f;

            for (int i = 0; i < towerCount; i++)
            {
                CreateTowerCard(i, availableTowers[i], startX + i * (CardWidth + CardSpacing));
            }
        }

        private void CreateTowerCard(int index, TowerData towerData, float xPos)
        {
            var card = new PieceCard
            {
                HandIndex = index,
                PathPreviewLines = new List<GameObject>(),
                IsModificationCard = false,
                IsTowerCard = true,
                TowerData = towerData
            };

            card.CardObject = new GameObject($"TowerCard_{towerData.towerName}");
            card.CardObject.transform.SetParent(panelRect);

            var rect = card.CardObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xPos, 0f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);

            card.Background = card.CardObject.AddComponent<Image>();
            card.Background.color = towerData.towerColor;

            // Selection border
            card.BorderObj = new GameObject("Border");
            card.BorderObj.transform.SetParent(card.CardObject.transform);
            var borderRect = card.BorderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-3f, -3f);
            borderRect.offsetMax = new Vector2(3f, 3f);
            card.BorderImage = card.BorderObj.AddComponent<Image>();
            card.BorderImage.color = new Color(1f, 0.9f, 0.3f);
            card.BorderImage.raycastTarget = false;
            card.BorderObj.SetActive(false);
            card.BorderObj.transform.SetAsFirstSibling();

            int capturedIndex = index;
            TowerData capturedData = towerData;
            var trigger = card.CardObject.AddComponent<EventTrigger>();
            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener((_) => OnTowerCardDragStart(capturedIndex, capturedData));
            trigger.triggers.Add(pointerDown);

            // Tower icon (colored square)
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(card.CardObject.transform);
            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.2f, 0.4f);
            iconRect.anchorMax = new Vector2(0.8f, 0.85f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImage = iconObj.AddComponent<Image>();
            if (towerData.towerIcon != null)
            {
                iconImage.sprite = towerData.towerIcon;
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
            }
            else
            {
                iconImage.color = towerData.towerColor * 0.7f;
            }
            iconImage.raycastTarget = false;

            // Name label
            GameObject labelObj = new GameObject("TypeLabel");
            labelObj.transform.SetParent(card.CardObject.transform);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.15f);
            labelRect.anchorMax = new Vector2(1f, 0.35f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            card.TypeLabel = labelObj.AddComponent<Text>();
            card.TypeLabel.text = towerData.towerName;
            card.TypeLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            card.TypeLabel.fontSize = 14;
            card.TypeLabel.color = Color.white;
            card.TypeLabel.alignment = TextAnchor.MiddleCenter;

            // Cost label
            GameObject costObj = new GameObject("CostLabel");
            costObj.transform.SetParent(card.CardObject.transform);
            var costRect = costObj.AddComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0f, 0f);
            costRect.anchorMax = new Vector2(1f, 0.15f);
            costRect.offsetMin = Vector2.zero;
            costRect.offsetMax = Vector2.zero;

            card.CostLabel = costObj.AddComponent<Text>();
            card.CostLabel.text = $"{towerData.cost}g";
            card.CostLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            card.CostLabel.fontSize = 12;
            card.CostLabel.color = new Color(1f, 0.85f, 0.3f);
            card.CostLabel.alignment = TextAnchor.MiddleCenter;

            cards.Add(card);
        }

        private void OnTowerCardDragStart(int index, TowerData towerData)
        {
            selectedIndex = index;
            UpdateBorders();
            HideTooltip();
            OnTowerCardSelected?.Invoke(towerData);
        }

        private void ShowTowerTooltip(TowerData data)
        {
            if (panelRect == null) return;

            if (tooltipObj == null)
            {
                tooltipObj = new GameObject("TowerTooltip");
                tooltipObj.transform.SetParent(panelRect);

                var rect = tooltipObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(10f, 0f);
                rect.sizeDelta = new Vector2(280f, 70f);

                var bg = tooltipObj.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.8f);

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(tooltipObj.transform);
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(8f, 4f);
                textRect.offsetMax = new Vector2(-8f, -4f);

                tooltipText = textObj.AddComponent<Text>();
                tooltipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tooltipText.fontSize = 14;
                tooltipText.color = Color.white;
                tooltipText.alignment = TextAnchor.MiddleLeft;
            }

            string desc = $"{data.towerName} | DMG: {data.damage} | Rate: {data.fireRate}/s | Range: {data.range}";
            if (data.isShotgun) desc += " | Shotgun";
            if (data.isTesla) desc += " | Chain Lightning";
            if (data.isFlame) desc += " | Fire";
            if (data.appliesSlow) desc += " | Slow Aura";
            tooltipText.text = desc;

            tooltipObj.SetActive(true);
        }

        private void RefreshModifications()
        {
            foreach (var card in cards)
            {
                if (card.CardObject != null)
                    Destroy(card.CardObject);
            }
            cards.Clear();
            selectedIndex = -1;

            int modCount = 4;
            CreatePanel(modCount);

            if (panelRect == null) return;

            var gm = GameManager.Instance;
            int mineCost = gm != null ? gm.GetMineCost() : 0;
            int lureCost = gm != null ? gm.GetLureCost() : 0;
            int hasteCost = gm != null ? gm.GetHasteCost() : 0;
            int goldenTouchCost = gm != null ? gm.GetGoldenTouchCost() : 0;

            float startX = -(modCount - 1) * (CardWidth + CardSpacing) / 2f + TabColumnWidth / 2f;

            CreateModificationCard(0, ModificationType.Mine, "Mine", mineCost,
                new Color(0.6f, 0.45f, 0.2f), startX);

            CreateModificationCard(1, ModificationType.Lure, "Lure", lureCost,
                new Color(0.8f, 0.7f, 0.2f), startX + (CardWidth + CardSpacing));

            CreateModificationCard(2, ModificationType.Haste, "Haste", hasteCost,
                new Color(0.9f, 0.4f, 0.1f), startX + 2 * (CardWidth + CardSpacing));

            CreateModificationCard(3, ModificationType.GoldenTouch, "Golden", goldenTouchCost,
                new Color(0.9f, 0.75f, 0.1f), startX + 3 * (CardWidth + CardSpacing));
        }

        private void CreateModificationCard(int index, ModificationType modType, string label, int cost, Color color, float xPos)
        {
            var card = new PieceCard
            {
                HandIndex = index,
                PathPreviewLines = new List<GameObject>(),
                IsModificationCard = true,
                ModType = modType
            };

            card.CardObject = new GameObject($"ModCard_{modType}");
            card.CardObject.transform.SetParent(panelRect);

            var rect = card.CardObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xPos, 0f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);

            card.Background = card.CardObject.AddComponent<Image>();
            card.Background.color = color;

            card.BorderObj = new GameObject("Border");
            card.BorderObj.transform.SetParent(card.CardObject.transform);
            var borderRect = card.BorderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-3f, -3f);
            borderRect.offsetMax = new Vector2(3f, 3f);
            card.BorderImage = card.BorderObj.AddComponent<Image>();
            card.BorderImage.color = new Color(1f, 0.9f, 0.3f);
            card.BorderImage.raycastTarget = false;
            card.BorderObj.SetActive(false);
            card.BorderObj.transform.SetAsFirstSibling();

            var button = card.CardObject.AddComponent<Button>();
            button.targetGraphic = card.Background;
            int capturedIndex = index;
            ModificationType capturedType = modType;
            button.onClick.AddListener(() => OnModificationCardClicked(capturedIndex, capturedType));

            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(card.CardObject.transform);
            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.2f, 0.4f);
            iconRect.anchorMax = new Vector2(0.8f, 0.85f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImage = iconObj.AddComponent<Image>();
            iconImage.color = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f);
            iconImage.raycastTarget = false;

            GameObject labelObj = new GameObject("TypeLabel");
            labelObj.transform.SetParent(card.CardObject.transform);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.15f);
            labelRect.anchorMax = new Vector2(1f, 0.35f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            card.TypeLabel = labelObj.AddComponent<Text>();
            card.TypeLabel.text = label;
            card.TypeLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            card.TypeLabel.fontSize = 14;
            card.TypeLabel.color = Color.white;
            card.TypeLabel.alignment = TextAnchor.MiddleCenter;

            GameObject costObj = new GameObject("CostLabel");
            costObj.transform.SetParent(card.CardObject.transform);
            var costRect = costObj.AddComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0f, 0f);
            costRect.anchorMax = new Vector2(1f, 0.15f);
            costRect.offsetMin = Vector2.zero;
            costRect.offsetMax = Vector2.zero;

            card.CostLabel = costObj.AddComponent<Text>();
            card.CostLabel.text = $"{cost}g";
            card.CostLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            card.CostLabel.fontSize = 12;
            card.CostLabel.color = new Color(1f, 0.85f, 0.3f);
            card.CostLabel.alignment = TextAnchor.MiddleCenter;

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
                if (card.CooldownOverlay == null) continue;

                float fraction = pieceProvider.GetCooldownFraction(i);
                bool onCooldown = fraction > 0f;

                card.CooldownOverlay.SetActive(onCooldown);

                if (onCooldown)
                {
                    var overlayRect = card.CooldownImage.rectTransform;
                    overlayRect.anchorMin = new Vector2(0f, 1f - fraction);
                    overlayRect.anchorMax = Vector2.one;

                    float remaining = pieceProvider.GetCooldownRemaining(i);
                    card.CooldownText.text = Mathf.CeilToInt(remaining).ToString();
                    card.CooldownText.gameObject.SetActive(true);
                }
                else
                {
                    card.CooldownText.gameObject.SetActive(false);
                }
            }
        }

        private void CreateCard(int index, HexPieceConfig config, float xPos)
        {
            var card = new PieceCard
            {
                HandIndex = index,
                PieceType = config.pieceType,
                PathPreviewLines = new List<GameObject>(),
                IsModificationCard = false
            };

            card.CardObject = new GameObject($"Card_{index}_{config.pieceType}");
            card.CardObject.transform.SetParent(panelRect);

            var rect = card.CardObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xPos, 0f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);

            card.Background = card.CardObject.AddComponent<Image>();
            card.Background.color = config.cardColor;

            card.BorderObj = new GameObject("Border");
            card.BorderObj.transform.SetParent(card.CardObject.transform);
            var borderRect = card.BorderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-3f, -3f);
            borderRect.offsetMax = new Vector2(3f, 3f);
            card.BorderImage = card.BorderObj.AddComponent<Image>();
            card.BorderImage.color = new Color(1f, 0.9f, 0.3f);
            card.BorderImage.raycastTarget = false;
            card.BorderObj.SetActive(index == selectedIndex);
            card.BorderObj.transform.SetAsFirstSibling();

            var button = card.CardObject.AddComponent<Button>();
            button.targetGraphic = card.Background;
            int capturedIndex = index;
            HexPieceType capturedType = config.pieceType;
            button.onClick.AddListener(() => OnCardClicked(capturedIndex, capturedType));

            CreatePathPreview(card, config);

            GameObject labelObj = new GameObject("TypeLabel");
            labelObj.transform.SetParent(card.CardObject.transform);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.15f);
            labelRect.anchorMax = new Vector2(1f, 0.35f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            card.TypeLabel = labelObj.AddComponent<Text>();
            card.TypeLabel.text = config.displayName;
            card.TypeLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            card.TypeLabel.fontSize = 14;
            card.TypeLabel.color = Color.white;
            card.TypeLabel.alignment = TextAnchor.MiddleCenter;

            GameObject costObj = new GameObject("CostLabel");
            costObj.transform.SetParent(card.CardObject.transform);
            var costRect = costObj.AddComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0f, 0f);
            costRect.anchorMax = new Vector2(1f, 0.15f);
            costRect.offsetMin = Vector2.zero;
            costRect.offsetMax = Vector2.zero;

            card.CostLabel = costObj.AddComponent<Text>();
            card.CostLabel.text = $"{config.placementCost}g";
            card.CostLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            card.CostLabel.fontSize = 12;
            card.CostLabel.color = new Color(1f, 0.85f, 0.3f);
            card.CostLabel.alignment = TextAnchor.MiddleCenter;

            // Cooldown overlay
            card.CooldownOverlay = new GameObject("CooldownOverlay");
            card.CooldownOverlay.transform.SetParent(card.CardObject.transform);
            var overlayRect = card.CooldownOverlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            card.CooldownImage = card.CooldownOverlay.AddComponent<Image>();
            card.CooldownImage.color = new Color(0f, 0f, 0f, 0.65f);
            card.CooldownImage.raycastTarget = false;
            card.CooldownOverlay.SetActive(false);

            GameObject timerObj = new GameObject("CooldownText");
            timerObj.transform.SetParent(card.CardObject.transform);
            var timerRect = timerObj.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0f, 0.3f);
            timerRect.anchorMax = new Vector2(1f, 0.8f);
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;

            card.CooldownText = timerObj.AddComponent<Text>();
            card.CooldownText.text = "";
            card.CooldownText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            card.CooldownText.fontSize = 24;
            card.CooldownText.fontStyle = FontStyle.Bold;
            card.CooldownText.color = Color.white;
            card.CooldownText.alignment = TextAnchor.MiddleCenter;
            timerObj.SetActive(false);

            cards.Add(card);
        }

        private void OnCardClicked(int index, HexPieceType type)
        {
            if (pieceProvider != null && !pieceProvider.IsReady(index))
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
            foreach (var card in cards)
            {
                if (card.BorderObj != null)
                    card.BorderObj.SetActive(card.HandIndex == selectedIndex);
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
            if (panelRect == null) return;

            if (tooltipObj == null)
            {
                tooltipObj = new GameObject("ModTooltip");
                tooltipObj.transform.SetParent(panelRect);

                var rect = tooltipObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(10f, 0f);
                rect.sizeDelta = new Vector2(280f, 70f);

                var bg = tooltipObj.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.8f);

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(tooltipObj.transform);
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(8f, 4f);
                textRect.offsetMax = new Vector2(-8f, -4f);

                tooltipText = textObj.AddComponent<Text>();
                tooltipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tooltipText.fontSize = 14;
                tooltipText.color = Color.white;
                tooltipText.alignment = TextAnchor.MiddleLeft;
            }

            if (ModDescriptions.TryGetValue(type, out var desc))
                tooltipText.text = desc;

            tooltipObj.SetActive(true);
        }

        private void HideTooltip()
        {
            if (tooltipObj != null)
                tooltipObj.SetActive(false);
        }

        private void CreatePathPreview(PieceCard card, HexPieceConfig config)
        {
            GameObject previewContainer = new GameObject("PathPreview");
            previewContainer.transform.SetParent(card.CardObject.transform);
            var containerRect = previewContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.1f, 0.35f);
            containerRect.anchorMax = new Vector2(0.9f, 0.9f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            List<float> angles = config.previewAngles;

            foreach (float angle in angles)
            {
                GameObject arm = new GameObject("Arm");
                arm.transform.SetParent(previewContainer.transform);
                var armRect = arm.AddComponent<RectTransform>();
                armRect.anchorMin = new Vector2(0.5f, 0.5f);
                armRect.anchorMax = new Vector2(0.5f, 0.5f);
                armRect.anchoredPosition = Vector2.zero;
                armRect.sizeDelta = new Vector2(6f, 22f);
                armRect.pivot = new Vector2(0.5f, 0f);
                armRect.localRotation = Quaternion.Euler(0f, 0f, angle);

                var armImage = arm.AddComponent<Image>();
                armImage.color = new Color(0.35f, 0.25f, 0.15f);

                card.PathPreviewLines.Add(arm);
            }
        }
    }
}
