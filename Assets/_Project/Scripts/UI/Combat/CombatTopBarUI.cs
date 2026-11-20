using System;
using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    /// <summary>
    /// Slay-the-Spire style Combat Top Bar matching the Figma layout:
    /// - Shared Party HP bar (with Shield &amp; Low-Will penalty visualization)
    /// - 3 Character Portrait Cards with individual Will sliders (&gt;=75% positive, &lt;25% Fear)
    ///   and per-character Buff/Debuff icon rows (hidden when empty, stacked with bottom-right count badges &amp; hover tooltips)
    /// - Center Relic slots, Crystal &amp; Gold counters, Map scroll button, and Menu button.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatTopBarUI : MonoBehaviour
    {
        [Serializable]
        public class CharacterWillSlot
        {
            public GameObject Root;
            public Image PortraitImage;
            public Image PortraitBorder;
            public Slider WillSlider;
            public Image WillFillImage;
            public TMP_Text WillStateBadge;
            public List<StatusCell> StatusCells = new();
        }

        [Serializable]
        public class StatusCell
        {
            public Image Background;
            public TMP_Text Label;

            [NonSerialized] public Image InnerBg;
            [NonSerialized] public Image IconImage;
            [NonSerialized] public TextMeshProUGUI StackBadge;
            [NonSerialized] public CombatStatusBadgeData BoundBadge;
            [NonSerialized] internal TopBarStatusHoverHandler HoverHandler;
            [NonSerialized] public string LastStackLabel;
            [NonSerialized] public bool VisualsBuilt;
        }

        [Header("Core References")]
        [SerializeField] private HeroView _heroView;
        [SerializeField] private CombatFeedbackBannerUI _feedbackBanner;
        [SerializeField] private CombatMapOverlayUI _mapOverlay;

        [Header("Top Bar HP & Currencies")]
        [SerializeField] private TMP_Text _hpText;
        [SerializeField] private Slider _hpSlider;
        [SerializeField] private Slider _shieldSlider;
        [SerializeField] private Slider _blockedSlider;
        [SerializeField] private TMP_Text _crystalText;
        [SerializeField] private TMP_Text _goldText;

        [Header("3 Character Portrait / Will / Buff Slots")]
        [SerializeField] private List<CharacterWillSlot> _characterSlots = new();

        [Header("Will Slider Colors")]
        [SerializeField] private Color _highWillFillColor = new(0.14f, 0.68f, 1f, 1f);
        [SerializeField] private Color _neutralWillFillColor = new(0.15f, 0.45f, 0.92f, 1f);
        [SerializeField] private Color _lowWillFillColor = new(0.66f, 0.11f, 0.15f, 1f);
        [SerializeField] private Color _normalBorderColor = new(0.78f, 0.65f, 0.36f, 1f);
        [SerializeField] private Color _highWillBorderColor = new(0.32f, 0.86f, 1f, 1f);
        [SerializeField] private Color _lowWillBorderColor = new(1f, 0.28f, 0.32f, 1f);

        private RunManager _subscribedRunManager;
        private BuffSystem _subscribedBuffSystem;
        private DamageOverTimeSystem _subscribedDotSystem;
        private StatusCell _hoveredCell;

        private void Awake()
        {
            ResolveHeroView();
        }

        private void OnEnable()
        {
            ResolveHeroView();
            SubscribeAll();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
            ClearHoverTooltip();
        }

        private void Update()
        {
            if (_subscribedRunManager != RunManager.Instance ||
                _subscribedBuffSystem != BuffSystem.Instance ||
                _subscribedDotSystem != DamageOverTimeSystem.Instance)
            {
                UnsubscribeAll();
                SubscribeAll();
                RefreshAll();
            }
        }

        public void OnMapButtonClicked()
        {
            if (_mapOverlay == null) _mapOverlay = FindFirstObjectByType<CombatMapOverlayUI>();
            if (_mapOverlay != null)
            {
                _mapOverlay.Toggle();
                return;
            }

            if (_feedbackBanner != null)
            {
                _feedbackBanner.ShowToast("Defeat enemies to advance on the Map.");
            }
        }

        public void RefreshAll()
        {
            RefreshHealth();
            RefreshCurrencies();
            RefreshCharacterWillSlots();
            RefreshStatusGrids();
        }

        /// <summary>
        /// Counts currently visible (active) status icon cells across all 3 character portrait rows.
        /// </summary>
        public int GetVisibleStatusCellCount(int slotIndex = -1)
        {
            if (_characterSlots == null) return 0;
            int total = 0;
            for (int i = 0; i < _characterSlots.Count; i++)
            {
                if (slotIndex >= 0 && i != slotIndex) continue;
                CharacterWillSlot slot = _characterSlots[i];
                if (slot?.StatusCells == null) continue;
                for (int c = 0; c < slot.StatusCells.Count; c++)
                {
                    if (slot.StatusCells[c]?.Background != null && slot.StatusCells[c].Background.gameObject.activeSelf)
                        total++;
                }
            }
            return total;
        }

        /// <summary>
        /// Triggers hover enter/exit programmatically for verification tests.
        /// </summary>
        public bool TryHoverStatusCell(int slotIndex, int badgeIndex)
        {
            if (_characterSlots == null || slotIndex < 0 || slotIndex >= _characterSlots.Count) return false;
            var cells = _characterSlots[slotIndex]?.StatusCells;
            if (cells == null || badgeIndex < 0 || badgeIndex >= cells.Count) return false;
            StatusCell cell = cells[badgeIndex];
            if (cell?.BoundBadge == null || cell.Background == null || !cell.Background.gameObject.activeSelf) return false;
            HandleCellHoverEnter(cell);
            return true;
        }

        public void ClearHoverTooltip()
        {
            if (_hoveredCell != null)
            {
                if (_hoveredCell.Background != null) _hoveredCell.Background.rectTransform.localScale = Vector3.one;
                _hoveredCell = null;
            }
            CombatStatusTooltipUI.Instance?.Hide(this);
        }

        private void RefreshHealth()
        {
            if (_heroView != null && _heroView.MaxHealth > 0)
            {
                int cur = _heroView.CurrentHealth;
                int max = _heroView.EffectiveMaxHealth;
                int shield = _heroView.CurrentShield;

                if (_hpText != null)
                {
                    _hpText.text = shield > 0
                        ? $"{cur}  /  {max}   <color=#7CE8FF>(+{shield})</color>"
                        : $"{cur}  /  {max}";
                }
                return;
            }

            if (RunManager.Instance != null && RunManager.Instance.HasHeroState)
            {
                if (_hpText != null)
                    _hpText.text = $"{RunManager.Instance.HeroCurrentHealth}  /  {RunManager.Instance.HeroMaxHealth}";
                if (_hpSlider != null)
                    _hpSlider.value = (float)RunManager.Instance.HeroCurrentHealth / Mathf.Max(1, RunManager.Instance.HeroMaxHealth);
            }
        }

        private void RefreshCurrencies()
        {
            int gold = RunManager.Instance != null ? RunManager.Instance.Gold : 150;
            int crystals = RunManager.Instance != null ? RunManager.Instance.Crystals : 100;

            if (_goldText != null) _goldText.text = gold.ToString();
            if (_crystalText != null) _crystalText.text = crystals.ToString();
        }

        private void RefreshCharacterWillSlots()
        {
            if (_characterSlots == null || _characterSlots.Count == 0) return;

            IReadOnlyList<RunHeroStressState> states = _heroView != null ? _heroView.StressStates : null;

            for (int i = 0; i < _characterSlots.Count; i++)
            {
                CharacterWillSlot slot = _characterSlots[i];
                if (slot == null) continue;

                RunHeroStressState state = null;
                if (states != null && states.Count > 0)
                {
                    state = i < states.Count ? states[i] : states[0];
                }

                float willNorm = state != null
                    ? state.WillNormalized
                    : (_heroView != null ? _heroView.WillNormalized : 0.5f);
                bool isHighWill = state != null ? state.IsHighWill : (_heroView != null && _heroView.IsHighWill);
                bool isLowWill = state != null ? state.IsLowWill : (_heroView != null && _heroView.IsLowWill);

                if (slot.WillSlider != null)
                {
                    slot.WillSlider.DOKill();
                    if (Application.isPlaying) slot.WillSlider.DOValue(willNorm, 0.22f).SetUpdate(true);
                    else slot.WillSlider.value = willNorm;
                }

                Color fillColor = isLowWill ? _lowWillFillColor : (isHighWill ? _highWillFillColor : _neutralWillFillColor);
                Color borderColor = isLowWill ? _lowWillBorderColor : (isHighWill ? _highWillBorderColor : _normalBorderColor);

                if (slot.WillFillImage != null) slot.WillFillImage.color = fillColor;
                if (slot.PortraitBorder != null) slot.PortraitBorder.color = borderColor;

                if (slot.WillStateBadge != null)
                {
                    int pct = Mathf.RoundToInt(willNorm * 100f);
                    if (isHighWill)
                        slot.WillStateBadge.text = $"<color=#64E8FF>{pct}% +</color>";
                    else if (isLowWill)
                        slot.WillStateBadge.text = $"<color=#FF6B6B>{pct}% !</color>";
                    else
                        slot.WillStateBadge.text = $"{pct}%";
                }
            }
        }

        private void RefreshStatusGrids()
        {
            if (_characterSlots == null || _characterSlots.Count == 0) return;

            List<CombatStatusBadgeData> sharedBadges = CombatStatusBadgeData.CollectFor(_heroView, includeWillState: true);
            bool hoveredStillValid = false;

            for (int i = 0; i < _characterSlots.Count; i++)
            {
                CharacterWillSlot slot = _characterSlots[i];
                if (slot == null || slot.StatusCells == null) continue;

                List<CombatStatusBadgeData> slotBadges = i == 0 ? sharedBadges : BuildSecondarySlotBadges(i);

                for (int c = 0; c < slot.StatusCells.Count; c++)
                {
                    StatusCell cell = slot.StatusCells[c];
                    if (cell == null || cell.Background == null) continue;

                    if (c < slotBadges.Count)
                    {
                        EnsureCellVisuals(cell);
                        CombatStatusBadgeData badge = slotBadges[c];
                        bool wasInactive = !cell.Background.gameObject.activeSelf;
                        bool stackedUp = !wasInactive && cell.BoundBadge != null && cell.BoundBadge.Id == badge.Id &&
                                         !string.IsNullOrEmpty(badge.StackLabel) && cell.LastStackLabel != badge.StackLabel;

                        cell.BoundBadge = badge;
                        cell.LastStackLabel = badge.StackLabel;
                        cell.Background.gameObject.SetActive(true);
                        BindStatusCellVisual(cell, badge);

                        if (Application.isPlaying)
                        {
                            if (wasInactive)
                            {
                                cell.Background.rectTransform.DOKill();
                                cell.Background.rectTransform.localScale = Vector3.one * 0.45f;
                                cell.Background.rectTransform.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack).SetUpdate(true);
                            }
                            else if (stackedUp && cell.StackBadge != null)
                            {
                                cell.StackBadge.rectTransform.DOKill();
                                cell.StackBadge.rectTransform.localScale = Vector3.one;
                                cell.StackBadge.rectTransform.DOPunchScale(Vector3.one * 0.35f, 0.22f, 6, 0.6f).SetUpdate(true);
                            }
                        }

                        if (_hoveredCell == cell)
                        {
                            hoveredStillValid = true;
                            CombatStatusTooltipUI.Instance?.Show(badge, cell.Background.rectTransform, this, preferBelow: true, playHoverSfx: false);
                        }
                    }
                    else
                    {
                        // Hide empty buff/debuff cells completely during gameplay — only show when an actual buff/debuff exists!
                        cell.BoundBadge = null;
                        cell.LastStackLabel = null;
                        cell.Background.gameObject.SetActive(false);
                    }
                }
            }

            if (_hoveredCell != null && !hoveredStillValid)
            {
                ClearHoverTooltip();
            }
        }

        private List<CombatStatusBadgeData> BuildSecondarySlotBadges(int slotIndex)
        {
            if (_heroView == null || _heroView.StressStates == null || _heroView.StressStates.Count == 0)
                return new List<CombatStatusBadgeData>();

            RunHeroStressState state = slotIndex < _heroView.StressStates.Count
                ? _heroView.StressStates[slotIndex]
                : _heroView.StressStates[0];

            return CombatStatusBadgeData.CollectWillOnly(state);
        }

        private void EnsureCellVisuals(StatusCell cell)
        {
            if (cell.VisualsBuilt || cell.Background == null) return;
            cell.VisualsBuilt = true;

            RectTransform rootRt = cell.Background.rectTransform;
            rootRt.sizeDelta = new Vector2(24f, 24f);
            cell.Background.raycastTarget = true;

            Sprite solid = CombatStatusTooltipUI.GetSolidSprite();
            TMP_FontAsset font = CombatStatusTooltipUI.Instance != null
                ? CombatStatusTooltipUI.Instance.Font
                : (cell.Label != null ? cell.Label.font : TMP_Settings.defaultFontAsset);
            int layer = rootRt.gameObject.layer;

            // Inner dark fill
            Transform existingInner = rootRt.Find("InnerBg");
            if (existingInner != null)
            {
                cell.InnerBg = existingInner.GetComponent<Image>();
            }
            else
            {
                GameObject innerGo = new("InnerBg", typeof(RectTransform), typeof(Image));
                innerGo.layer = layer;
                RectTransform innerRt = (RectTransform)innerGo.transform;
                innerRt.SetParent(rootRt, false);
                innerRt.SetAsFirstSibling();
                innerRt.anchorMin = Vector2.zero;
                innerRt.anchorMax = Vector2.one;
                innerRt.offsetMin = new Vector2(1.5f, 1.5f);
                innerRt.offsetMax = new Vector2(-1.5f, -1.5f);
                cell.InnerBg = innerGo.GetComponent<Image>();
                cell.InnerBg.sprite = solid;
                cell.InnerBg.raycastTarget = false;
            }

            // Icon sprite inside badge
            Transform existingIcon = rootRt.Find("StatusIcon");
            if (existingIcon != null)
            {
                cell.IconImage = existingIcon.GetComponent<Image>();
            }
            else
            {
                GameObject iconGo = new("StatusIcon", typeof(RectTransform), typeof(Image));
                iconGo.layer = layer;
                RectTransform iconRt = (RectTransform)iconGo.transform;
                iconRt.SetParent(rootRt, false);
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = new Vector2(3.5f, 3.5f);
                iconRt.offsetMax = new Vector2(-3.5f, -3.5f);
                cell.IconImage = iconGo.GetComponent<Image>();
                cell.IconImage.preserveAspect = true;
                cell.IconImage.raycastTarget = false;
            }

            // Stack count badge tucked at the bottom-right corner, right under the icon
            Transform existingStack = rootRt.Find("StackBadge");
            if (existingStack != null)
            {
                cell.StackBadge = existingStack.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                GameObject stackGo = new("StackBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
                stackGo.layer = layer;
                RectTransform stackRt = (RectTransform)stackGo.transform;
                stackRt.SetParent(rootRt, false);
                stackRt.anchorMin = stackRt.anchorMax = new Vector2(1f, 0f);
                stackRt.pivot = new Vector2(0.80f, 0.25f);
                stackRt.anchoredPosition = new Vector2(3f, -2f);
                stackRt.sizeDelta = new Vector2(28f, 16f);

                cell.StackBadge = stackGo.GetComponent<TextMeshProUGUI>();
                cell.StackBadge.fontSize = 12.5f;
                cell.StackBadge.fontStyle = FontStyles.Bold;
                cell.StackBadge.alignment = TextAlignmentOptions.BottomRight;
                cell.StackBadge.color = Color.white;
                cell.StackBadge.textWrappingMode = TextWrappingModes.NoWrap;
                cell.StackBadge.raycastTarget = false;
                if (font != null) cell.StackBadge.font = font;

                Outline outline = stackGo.AddComponent<Outline>();
                outline.effectColor = new Color(0.02f, 0.03f, 0.06f, 0.98f);
                outline.effectDistance = new Vector2(1.4f, -1.4f);
            }

            // Hover handler for status description tooltip
            cell.HoverHandler = rootRt.GetComponent<TopBarStatusHoverHandler>();
            if (cell.HoverHandler == null) cell.HoverHandler = rootRt.gameObject.AddComponent<TopBarStatusHoverHandler>();
            cell.HoverHandler.Init(this, cell);
        }

        private static void BindStatusCellVisual(StatusCell cell, CombatStatusBadgeData badge)
        {
            Color accent = badge.AccentColor;
            cell.Background.color = accent;

            if (cell.InnerBg != null)
            {
                cell.InnerBg.color = new Color(accent.r * 0.18f + 0.06f, accent.g * 0.18f + 0.06f, accent.b * 0.18f + 0.08f, 0.96f);
            }

            Sprite icon = CombatStatusTooltipUI.Instance != null
                ? CombatStatusTooltipUI.Instance.GetSprite(badge.IconKey)
                : null;

            bool hasIcon = icon != null;
            if (cell.IconImage != null)
            {
                cell.IconImage.sprite = icon;
                cell.IconImage.enabled = hasIcon;
                cell.IconImage.color = Color.Lerp(accent, Color.white, 0.24f);
            }

            if (cell.Label != null)
            {
                cell.Label.gameObject.SetActive(!hasIcon);
                if (!hasIcon) cell.Label.text = badge.ShortCode;
            }

            if (cell.StackBadge != null)
            {
                bool showStack = !string.IsNullOrEmpty(badge.StackLabel);
                cell.StackBadge.gameObject.SetActive(showStack);
                if (showStack)
                {
                    cell.StackBadge.text = badge.StackLabel;
                    cell.StackBadge.transform.SetAsLastSibling();
                }
            }
        }

        internal void HandleCellHoverEnter(StatusCell cell)
        {
            if (cell == null || cell.BoundBadge == null || cell.Background == null) return;
            if (_hoveredCell != null && _hoveredCell != cell && _hoveredCell.Background != null)
            {
                _hoveredCell.Background.rectTransform.localScale = Vector3.one;
            }

            _hoveredCell = cell;
            cell.Background.rectTransform.localScale = Vector3.one * 1.15f;
            CombatStatusTooltipUI.Instance?.Show(cell.BoundBadge, cell.Background.rectTransform, this, preferBelow: true);
        }

        internal void HandleCellHoverExit(StatusCell cell)
        {
            if (cell != null && cell.Background != null)
            {
                cell.Background.rectTransform.localScale = Vector3.one;
            }

            if (_hoveredCell == cell)
            {
                _hoveredCell = null;
                CombatStatusTooltipUI.Instance?.Hide(this);
            }
        }

        private void ResolveHeroView()
        {
            if (_heroView == null && HeroSystem.Instance != null) _heroView = HeroSystem.Instance.HeroView;
            if (_heroView == null) _heroView = FindFirstObjectByType<HeroView>();
        }

        private void SubscribeAll()
        {
            if (_heroView != null)
            {
                _heroView.HealthChanged -= RefreshHealth;
                _heroView.HealthChanged += RefreshHealth;
                _heroView.StressStateChanged -= RefreshWillAndStatuses;
                _heroView.StressStateChanged += RefreshWillAndStatuses;
                _heroView.StatusEffectsChanged -= RefreshStatusGrids;
                _heroView.StatusEffectsChanged += RefreshStatusGrids;
            }

            _subscribedRunManager = RunManager.Instance;
            if (_subscribedRunManager != null)
            {
                _subscribedRunManager.RunStateChanged -= RefreshCurrencies;
                _subscribedRunManager.RunStateChanged += RefreshCurrencies;
            }

            _subscribedBuffSystem = BuffSystem.Instance;
            if (_subscribedBuffSystem != null)
            {
                _subscribedBuffSystem.StatusEffectsChanged -= RefreshStatusGrids;
                _subscribedBuffSystem.StatusEffectsChanged += RefreshStatusGrids;
            }

            _subscribedDotSystem = DamageOverTimeSystem.Instance;
            if (_subscribedDotSystem != null)
            {
                _subscribedDotSystem.StatusEffectsChanged -= RefreshStatusGrids;
                _subscribedDotSystem.StatusEffectsChanged += RefreshStatusGrids;
            }
        }

        private void UnsubscribeAll()
        {
            if (_heroView != null)
            {
                _heroView.HealthChanged -= RefreshHealth;
                _heroView.StressStateChanged -= RefreshWillAndStatuses;
                _heroView.StatusEffectsChanged -= RefreshStatusGrids;
            }
            if (_subscribedRunManager != null)
            {
                _subscribedRunManager.RunStateChanged -= RefreshCurrencies;
                _subscribedRunManager = null;
            }
            if (_subscribedBuffSystem != null)
            {
                _subscribedBuffSystem.StatusEffectsChanged -= RefreshStatusGrids;
                _subscribedBuffSystem = null;
            }
            if (_subscribedDotSystem != null)
            {
                _subscribedDotSystem.StatusEffectsChanged -= RefreshStatusGrids;
                _subscribedDotSystem = null;
            }
        }

        private void RefreshWillAndStatuses()
        {
            RefreshCharacterWillSlots();
            RefreshStatusGrids();
        }
    }

    internal sealed class TopBarStatusHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private CombatTopBarUI _owner;
        private CombatTopBarUI.StatusCell _cell;

        public void Init(CombatTopBarUI owner, CombatTopBarUI.StatusCell cell)
        {
            _owner = owner;
            _cell = cell;
        }

        public void OnPointerEnter(PointerEventData eventData) => _owner?.HandleCellHoverEnter(_cell);
        public void OnPointerExit(PointerEventData eventData) => _owner?.HandleCellHoverExit(_cell);
    }
}
