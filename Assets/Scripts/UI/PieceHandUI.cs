using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Grid;
using TowerDefense.Data;
using TowerDefense.Core;

namespace TowerDefense.UI
{
    public enum ModificationType { Mine, Lure }

    public class PieceHandUI : MonoBehaviour
    {
        private enum HandTab { Paths, Modifications }

        private RectTransform panelRect;
        private List<PieceCard> cards = new List<PieceCard>();
        private Canvas parentCanvas;
        private int selectedIndex = -1;
        private Dictionary<HexPieceType, HexPieceConfig> pieceConfigs;
        private PieceProvider pieceProvider;

        private HandTab activeTab = HandTab.Paths;
        private GameObject pathsTabBtn;
        private GameObject modsTabBtn;
        private Image pathsTabImage;
        private Image modsTabImage;

        private static readonly Color TabActiveColor = new Color(0.3f, 0.6f, 0.3f);
        private static readonly Color TabInactiveColor = new Color(0.25f, 0.25f, 0.25f);

        private const float CardWidth = 90f;
        private const float CardHeight = 110f;
        private const float CardSpacing = 10f;
        private const float PanelPadding = 15f;
        private const float TabColumnWidth = 50f;

        public event Action<int, HexPieceType> OnCardSelected;
        public event Action OnCardDeselected;
        public event Action<ModificationType> OnModificationSelected;
        public event Action OnModificationDeselected;

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
        }

        public void Initialize(Canvas canvas)
        {
            parentCanvas = canvas;
        }

        public void SetPieceConfigs(Dictionary<HexPieceType, HexPieceConfig> configs)
        {
            pieceConfigs = configs;
        }

        public void SetPieceProvider(PieceProvider provider)
        {
            pieceProvider = provider;
        }

        private void CreatePanel(int pieceCount)
        {
            if (panelRect != null)
                Destroy(panelRect.gameObject);

            GameObject panel = new GameObject("PieceHandPanel");
            panel.transform.SetParent(transform);

            panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);

            float cardsWidth = pieceCount * CardWidth + (pieceCount - 1) * CardSpacing + PanelPadding * 2f;
            float totalWidth = cardsWidth + TabColumnWidth;
            float panelHeight = CardHeight + PanelPadding * 2f;
            panelRect.sizeDelta = new Vector2(totalWidth, panelHeight);
            panelRect.anchoredPosition = new Vector2(0f, panelHeight / 2f + 10f);

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            // Create tab column on the left side
            CreateTabColumn(panel.transform, panelHeight);
        }

        private void CreateTabColumn(Transform parent, float panelHeight)
        {
            // Destroy old tabs if they exist (they're children of the panel which was just destroyed, so this is just cleanup)
            pathsTabBtn = null;
            modsTabBtn = null;

            float tabHeight = (panelHeight - 6f) / 2f; // 2px gap between tabs, 2px top/bottom padding

            // Paths tab button
            pathsTabBtn = new GameObject("PathsTab");
            pathsTabBtn.transform.SetParent(parent);
            var pathsRect = pathsTabBtn.AddComponent<RectTransform>();
            pathsRect.anchorMin = new Vector2(0f, 0.5f);
            pathsRect.anchorMax = new Vector2(0f, 0.5f);
            pathsRect.anchoredPosition = new Vector2(TabColumnWidth / 2f + 2f, tabHeight / 4f + 1f);
            pathsRect.sizeDelta = new Vector2(TabColumnWidth - 4f, tabHeight - 2f);

            pathsTabImage = pathsTabBtn.AddComponent<Image>();
            var pathsButton = pathsTabBtn.AddComponent<Button>();
            pathsButton.targetGraphic = pathsTabImage;
            pathsButton.onClick.AddListener(() => SwitchTab(HandTab.Paths));

            var pathsTextObj = new GameObject("Text");
            pathsTextObj.transform.SetParent(pathsTabBtn.transform);
            var pathsTextRect = pathsTextObj.AddComponent<RectTransform>();
            pathsTextRect.anchorMin = Vector2.zero;
            pathsTextRect.anchorMax = Vector2.one;
            pathsTextRect.offsetMin = Vector2.zero;
            pathsTextRect.offsetMax = Vector2.zero;
            var pathsText = pathsTextObj.AddComponent<Text>();
            pathsText.text = "Path";
            pathsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pathsText.fontSize = 11;
            pathsText.color = Color.white;
            pathsText.alignment = TextAnchor.MiddleCenter;

            // Mods tab button
            modsTabBtn = new GameObject("ModsTab");
            modsTabBtn.transform.SetParent(parent);
            var modsRect = modsTabBtn.AddComponent<RectTransform>();
            modsRect.anchorMin = new Vector2(0f, 0.5f);
            modsRect.anchorMax = new Vector2(0f, 0.5f);
            modsRect.anchoredPosition = new Vector2(TabColumnWidth / 2f + 2f, -(tabHeight / 4f + 1f));
            modsRect.sizeDelta = new Vector2(TabColumnWidth - 4f, tabHeight - 2f);

            modsTabImage = modsTabBtn.AddComponent<Image>();
            var modsButton = modsTabBtn.AddComponent<Button>();
            modsButton.targetGraphic = modsTabImage;
            modsButton.onClick.AddListener(() => SwitchTab(HandTab.Modifications));

            var modsTextObj = new GameObject("Text");
            modsTextObj.transform.SetParent(modsTabBtn.transform);
            var modsTextRect = modsTextObj.AddComponent<RectTransform>();
            modsTextRect.anchorMin = Vector2.zero;
            modsTextRect.anchorMax = Vector2.one;
            modsTextRect.offsetMin = Vector2.zero;
            modsTextRect.offsetMax = Vector2.zero;
            var modsText = modsTextObj.AddComponent<Text>();
            modsText.text = "Mods";
            modsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            modsText.fontSize = 11;
            modsText.color = Color.white;
            modsText.alignment = TextAnchor.MiddleCenter;

            UpdateTabColors();
        }

        private void UpdateTabColors()
        {
            if (pathsTabImage != null)
                pathsTabImage.color = activeTab == HandTab.Paths ? TabActiveColor : TabInactiveColor;
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
            else if (activeTab == HandTab.Modifications)
            {
                selectedIndex = -1;
                // Always fire deselect to ensure highlights are cleared
                OnModificationDeselected?.Invoke();
            }

            activeTab = tab;
            UpdateTabColors();

            if (tab == HandTab.Paths)
            {
                // Force recreation by clearing cards
                foreach (var card in cards)
                {
                    if (card.CardObject != null)
                        Destroy(card.CardObject);
                }
                cards.Clear();

                if (pieceProvider != null)
                    RefreshHand(pieceProvider.Pieces);
            }
            else
            {
                RefreshModifications();
            }
        }

        public void RefreshHand(IReadOnlyList<HexPieceConfig> pieces)
        {
            if (activeTab != HandTab.Paths) return;

            // Only recreate cards if piece count changed (first call or config change)
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

            // If selected index is out of range, deselect
            if (selectedIndex >= pieces.Count)
            {
                selectedIndex = -1;
                OnCardDeselected?.Invoke();
            }

            UpdateBorders();
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

            CreatePanel(2);

            if (panelRect == null) return;

            var gm = GameManager.Instance;
            int mineCost = gm != null ? gm.GetMineCost() : 0;
            int lureCost = gm != null ? gm.GetLureCost() : 0;

            float startX = -(2 - 1) * (CardWidth + CardSpacing) / 2f + TabColumnWidth / 2f;

            // Mine card
            CreateModificationCard(0, ModificationType.Mine, "Mine", mineCost,
                new Color(0.6f, 0.45f, 0.2f), startX);

            // Lure card
            CreateModificationCard(1, ModificationType.Lure, "Lure", lureCost,
                new Color(0.8f, 0.7f, 0.2f), startX + (CardWidth + CardSpacing));
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

            // Card container
            card.CardObject = new GameObject($"ModCard_{modType}");
            card.CardObject.transform.SetParent(panelRect);

            var rect = card.CardObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xPos, 0f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);

            // Background
            card.Background = card.CardObject.AddComponent<Image>();
            card.Background.color = color;

            // Selection border (hidden by default)
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

            // Click button
            var button = card.CardObject.AddComponent<Button>();
            button.targetGraphic = card.Background;
            int capturedIndex = index;
            ModificationType capturedType = modType;
            button.onClick.AddListener(() => OnModificationCardClicked(capturedIndex, capturedType));

            // Icon area (simple colored square representing the modification)
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

            // Type label
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

            // Cost label
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
                OnModificationDeselected?.Invoke();
            }
            else
            {
                selectedIndex = index;
                UpdateBorders();
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
                    // Shrink overlay from top: full height when fraction=1, zero when fraction=0
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

            // Card container
            card.CardObject = new GameObject($"Card_{index}_{config.pieceType}");
            card.CardObject.transform.SetParent(panelRect);

            var rect = card.CardObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xPos, 0f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);

            // Background
            card.Background = card.CardObject.AddComponent<Image>();
            card.Background.color = config.cardColor;

            // Selection border (hidden by default)
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

            // Click button
            var button = card.CardObject.AddComponent<Button>();
            button.targetGraphic = card.Background;
            int capturedIndex = index;
            HexPieceType capturedType = config.pieceType;
            button.onClick.AddListener(() => OnCardClicked(capturedIndex, capturedType));

            // Path preview
            CreatePathPreview(card, config);

            // Type label
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

            // Cost label
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

            // Cooldown timer text (centered on card, child of card not overlay so it's always visible)
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
            // Ignore clicks on cards that are on cooldown
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
