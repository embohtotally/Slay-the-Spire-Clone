using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.UI.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    /// <summary>
    /// Screen-space Enemy Intent Icon HUD matching Slay the Spire &amp; Witch's Apocalyptic Journey:
    /// - Replaceable icon templates per EnemyIntentCategory (assign custom sprites in Inspector anytime)
    /// - Color-coded action label displayed at the top of each intent icon
    /// - Live buff-adjusted primary damage/shield/value numbers &amp; optional secondary chip (e.g. "-5 WILL")
    /// - Procedural squash-and-stretch pop-in animation at Player Turn start, alive idle motion, and execute burst
    /// - Non-blocking cursor hover detection that shows a rich intent description tooltip
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyIntentHUD : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private CombatHandController _handController;

        [Header("Intent Icon Templates (Swap with Custom Art Anytime)")]
        [SerializeField] private Sprite _attackIcon;
        [SerializeField] private Sprite _attackDebuffIcon;
        [SerializeField] private Sprite _attackBuffIcon;
        [SerializeField] private Sprite _defendIcon;
        [SerializeField] private Sprite _buffIcon;
        [SerializeField] private Sprite _debuffIcon;
        [SerializeField] private Sprite _willDrainIcon;
        [SerializeField] private Sprite _healIcon;
        [SerializeField] private Sprite _summonIcon;
        [SerializeField] private Sprite _stunnedIcon;
        [SerializeField] private Sprite _unknownIcon;

        [Header("Layout & Procedural Animation")]
        [SerializeField] private float _verticalOffsetPx = 12f;
        [SerializeField] private float _topSafeMarginPx = 108f;
        [SerializeField] private float _hoverPaddingPx = 10f;
        [SerializeField] private float _hoverScale = 1.12f;

        private sealed class IntentWidget
        {
            public EnemyView Enemy;
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform AnimPivot;
            public RectTransform HoverBounds;

            public Image TopPillBorder;
            public Image TopPillBg;
            public TextMeshProUGUI TopActionLabel;

            public Image BurstRing;
            public Image IconBorder;
            public Image IconBg;
            public Image IconImage;
            public TextMeshProUGUI ValueText;

            public GameObject SecondaryChipRoot;
            public Image SecondaryChipBorder;
            public TextMeshProUGUI SecondaryChipText;

            public EnemyIntentSnapshot Snapshot;
            public Sequence ActiveTween;
            public Tween BurstTween;
            public Tween ValuePunchTween;

            public string LastValueText;
            public float IdlePhase;
            public float HoverBlend;
            public bool IsAnimatingTransition;
            public bool IsVisible;
        }

        private static Sprite _solidSprite;

        private RectTransform _widgetsLayer;
        private RectTransform _tooltipRoot;
        private CanvasGroup _tooltipGroup;
        private Image _tooltipAccentBar;
        private Image _tooltipBorder;
        private TextMeshProUGUI _tooltipEnemyName;
        private TextMeshProUGUI _tooltipTitle;
        private TextMeshProUGUI _tooltipBody;

        private readonly Dictionary<EnemyView, IntentWidget> _widgets = new();
        private readonly List<EnemyView> _staleKeys = new();

        private EnemyView _hoveredEnemy;
        private Vector2? _simulatedHoverScreenPos;
        private RelicSwapModalUI _relicSwapModal;

        public EnemyView HoveredEnemy => _hoveredEnemy;
        public bool IsTooltipVisible => _tooltipRoot != null && _tooltipRoot.gameObject.activeSelf;

        /// <summary>
        /// Allows automated tests to hover an exact screen coordinate through the real hit-test path.
        /// Pass null to return to live Mouse.current input.
        /// </summary>
        public void SetSimulatedHoverScreenPosition(Vector2? screenPos)
        {
            _simulatedHoverScreenPos = screenPos;
        }

        /// <summary>
        /// Returns the current screen-space center of an enemy's active intent widget (for inspection/tests).
        /// </summary>
        public bool TryGetIntentScreenCenter(EnemyView enemy, out Vector2 screenCenter)
        {
            screenCenter = Vector2.zero;
            if (enemy == null || !_widgets.TryGetValue(enemy, out IntentWidget w) || !w.IsVisible || w.HoverBounds == null)
                return false;

            Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            Vector3 worldCenter = w.HoverBounds.TransformPoint(w.HoverBounds.rect.center);
            screenCenter = RectTransformUtility.WorldToScreenPoint(uiCam, worldCenter);
            return true;
        }

        private void Awake()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_worldCamera == null) _worldCamera = Camera.main;
            if (_handController == null) _handController = FindFirstObjectByType<CombatHandController>();
            _relicSwapModal = FindFirstObjectByType<RelicSwapModalUI>();
            BuildLayersIfNeeded();
        }

        private void OnEnable()
        {
            EnemyView.SuppressLegacyIntentText = true;
            EnemyView.AnyIntentRevealed -= HandleIntentRevealed;
            EnemyView.AnyIntentRevealed += HandleIntentRevealed;
            EnemyView.AnyIntentUpdated -= HandleIntentUpdated;
            EnemyView.AnyIntentUpdated += HandleIntentUpdated;
            EnemyView.AnyIntentConsumed -= HandleIntentConsumed;
            EnemyView.AnyIntentConsumed += HandleIntentConsumed;

            BuildLayersIfNeeded();
            SyncExistingEnemies();
        }

        private void OnDisable()
        {
            EnemyView.SuppressLegacyIntentText = false;
            EnemyView.AnyIntentRevealed -= HandleIntentRevealed;
            EnemyView.AnyIntentUpdated -= HandleIntentUpdated;
            EnemyView.AnyIntentConsumed -= HandleIntentConsumed;

            foreach (IntentWidget w in _widgets.Values)
            {
                w.ActiveTween?.Kill();
                w.BurstTween?.Kill();
                w.ValuePunchTween?.Kill();
            }

            HideTooltip();
        }

        private void LateUpdate()
        {
            if (_worldCamera == null) _worldCamera = Camera.main;
            if (_canvas == null || _widgetsLayer == null) return;

            SyncEnemyRoster();
            UpdateWidgetPositionsAndIdle();
            UpdateHoverTooltip();
        }

        // ------------------------------------------------------------------
        // Enemy Event Handlers
        // ------------------------------------------------------------------
        private void HandleIntentRevealed(EnemyView enemy, float revealDelay)
        {
            if (enemy == null || !enemy.IsIntentVisible || enemy.CurrentIntentSnapshot == null) return;

            IntentWidget widget = GetOrCreateWidget(enemy);
            BindSnapshot(widget, enemy.CurrentIntentSnapshot, punchValueOnChange: false);
            PlayPopIn(widget, revealDelay);
        }

        private void HandleIntentUpdated(EnemyView enemy)
        {
            if (enemy == null) return;

            if (!enemy.IsIntentVisible || enemy.CurrentIntentSnapshot == null)
            {
                if (_widgets.TryGetValue(enemy, out IntentWidget hiddenWidget) && hiddenWidget.IsVisible)
                {
                    PlayConsumeAndHide(hiddenWidget);
                }
                return;
            }

            IntentWidget widget = GetOrCreateWidget(enemy);
            bool wasHidden = !widget.IsVisible;
            BindSnapshot(widget, enemy.CurrentIntentSnapshot, punchValueOnChange: !wasHidden);

            if (wasHidden)
            {
                PlayPopIn(widget, 0f);
            }
            else if (_hoveredEnemy == enemy)
            {
                PopulateTooltip(widget.Snapshot);
            }
        }

        private void HandleIntentConsumed(EnemyView enemy)
        {
            if (enemy == null) return;
            if (_hoveredEnemy == enemy)
            {
                _hoveredEnemy = null;
                HideTooltip();
            }

            if (_widgets.TryGetValue(enemy, out IntentWidget widget))
            {
                PlayConsumeAndHide(widget);
            }
        }

        private void SyncExistingEnemies()
        {
            if (EnemySystem.Instance == null || EnemySystem.Instance.Enemies == null) return;

            int order = 0;
            foreach (EnemyView enemy in EnemySystem.Instance.Enemies)
            {
                if (enemy == null) continue;
                enemy.RefreshIntentDisplay();

                if (enemy.IsIntentVisible && enemy.CurrentIntentSnapshot != null)
                {
                    IntentWidget w = GetOrCreateWidget(enemy);
                    BindSnapshot(w, enemy.CurrentIntentSnapshot, punchValueOnChange: false);
                    PlayPopIn(w, 0.10f + order * 0.10f);
                    order++;
                }
            }
        }

        private void SyncEnemyRoster()
        {
            // Clean up widgets whose enemy has been destroyed or killed
            _staleKeys.Clear();
            foreach (KeyValuePair<EnemyView, IntentWidget> pair in _widgets)
            {
                if (pair.Key == null || pair.Key.CurrentHealth <= 0)
                {
                    _staleKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < _staleKeys.Count; i++)
            {
                EnemyView stale = _staleKeys[i];
                if (_hoveredEnemy == stale)
                {
                    _hoveredEnemy = null;
                    HideTooltip();
                }

                if (_widgets.TryGetValue(stale, out IntentWidget w))
                {
                    w.ActiveTween?.Kill();
                    w.BurstTween?.Kill();
                    w.ValuePunchTween?.Kill();
                    if (w.Root != null) Destroy(w.Root.gameObject);
                    _widgets.Remove(stale);
                }
            }

            // Pick up any newly spawned enemies that haven't registered a widget yet
            if (EnemySystem.Instance != null && EnemySystem.Instance.Enemies != null)
            {
                foreach (EnemyView enemy in EnemySystem.Instance.Enemies)
                {
                    if (enemy == null || enemy.CurrentHealth <= 0) continue;
                    if (enemy.IsIntentVisible && enemy.CurrentIntentSnapshot != null && !_widgets.ContainsKey(enemy))
                    {
                        IntentWidget w = GetOrCreateWidget(enemy);
                        BindSnapshot(w, enemy.CurrentIntentSnapshot, punchValueOnChange: false);
                        PlayPopIn(w, 0.05f);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Positioning & Procedural Idle Motion
        // ------------------------------------------------------------------
        private void UpdateWidgetPositionsAndIdle()
        {
            if (_worldCamera == null || _canvas.transform is not RectTransform canvasRect) return;

            Camera uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            float halfH = canvasRect.rect.height * 0.5f;
            float halfW = canvasRect.rect.width * 0.5f;
            float maxY = halfH - _topSafeMarginPx;
            float minY = -halfH + 140f;
            float time = Time.unscaledTime;

            foreach (IntentWidget w in _widgets.Values)
            {
                if (w.Enemy == null || w.Root == null || !w.Root.gameObject.activeSelf) continue;

                Vector3 worldAnchor = w.Enemy.GetIntentAnchorWorldPosition();
                Vector3 screenPt = _worldCamera.WorldToScreenPoint(worldAnchor);

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPt, uiCam, out Vector2 localPt))
                {
                    localPt.y = Mathf.Clamp(localPt.y + _verticalOffsetPx, minY, maxY);
                    localPt.x = Mathf.Clamp(localPt.x, -halfW + 100f, halfW - 100f);
                    w.Root.anchoredPosition = localPt;
                }

                // Smooth hover magnification blend
                float targetHover = _hoveredEnemy == w.Enemy ? 1f : 0f;
                w.HoverBlend = Mathf.MoveTowards(w.HoverBlend, targetHover, Time.unscaledDeltaTime * 9f);

                if (w.IsAnimatingTransition || !w.IsVisible) continue;

                // Category-aware procedural alive idle motion
                EnemyIntentCategory cat = w.Snapshot != null ? w.Snapshot.Category : EnemyIntentCategory.Unknown;
                float bob = Mathf.Sin(time * 2.5f + w.IdlePhase) * 3.5f;
                float rotZ = 0f;
                float idlePulse = 1f;

                switch (cat)
                {
                    case EnemyIntentCategory.Attack:
                    case EnemyIntentCategory.AttackDebuff:
                    case EnemyIntentCategory.AttackBuff:
                        idlePulse = 1f + Mathf.Sin(time * 4.4f + w.IdlePhase) * 0.035f;
                        bob = Mathf.Sin(time * 3.0f + w.IdlePhase) * 3.0f;
                        break;

                    case EnemyIntentCategory.Debuff:
                    case EnemyIntentCategory.WillDrain:
                        rotZ = Mathf.Sin(time * 2.4f + w.IdlePhase) * 3.2f;
                        break;

                    case EnemyIntentCategory.Stunned:
                        rotZ = Mathf.Sin(time * 5.2f + w.IdlePhase) * 6.0f;
                        bob = Mathf.Sin(time * 3.8f + w.IdlePhase) * 2.5f;
                        break;
                }

                float scale = idlePulse * Mathf.Lerp(1f, _hoverScale, w.HoverBlend);
                w.AnimPivot.anchoredPosition = new Vector2(0f, bob + w.HoverBlend * 4f);
                w.AnimPivot.localScale = new Vector3(scale, scale, 1f);
                w.AnimPivot.localRotation = Quaternion.Euler(0f, 0f, rotZ * (1f - w.HoverBlend));
            }
        }

        // ------------------------------------------------------------------
        // Procedural Pop-In & Execute Consume Animations
        // ------------------------------------------------------------------
        private void PlayPopIn(IntentWidget w, float delay)
        {
            w.ActiveTween?.Kill();
            w.BurstTween?.Kill();

            w.IsVisible = true;
            w.IsAnimatingTransition = true;
            w.Root.gameObject.SetActive(true);

            // Initial anticipation pose (hidden & vertically stretched slightly below target)
            w.Group.alpha = 0f;
            w.AnimPivot.anchoredPosition = new Vector2(0f, -26f);
            w.AnimPivot.localScale = new Vector3(0.32f, 1.38f, 1f);
            w.AnimPivot.localRotation = Quaternion.Euler(0f, 0f, -10f);

            if (w.BurstRing != null)
            {
                w.BurstRing.gameObject.SetActive(false);
            }

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            if (delay > 0f)
            {
                seq.AppendInterval(delay);
            }

            seq.AppendCallback(() => PlayBurstFlash(w, 0.52f, 1.55f, 0.36f));

            // Phase 1: Energetic upward pop with squash overshoot
            seq.Append(w.Group.DOFade(1f, 0.14f).SetEase(Ease.OutQuad));
            seq.Join(w.AnimPivot.DOAnchorPosY(12f, 0.17f).SetEase(Ease.OutCubic));
            seq.Join(w.AnimPivot.DOScale(new Vector3(1.18f, 0.86f, 1f), 0.17f).SetEase(Ease.OutCubic));
            seq.Join(w.AnimPivot.DOLocalRotate(new Vector3(0f, 0f, 3.5f), 0.17f).SetEase(Ease.OutCubic));

            // Phase 2: Spring rebound
            seq.Append(w.AnimPivot.DOAnchorPosY(-3f, 0.12f).SetEase(Ease.InOutSine));
            seq.Join(w.AnimPivot.DOScale(new Vector3(0.95f, 1.06f, 1f), 0.12f).SetEase(Ease.InOutSine));
            seq.Join(w.AnimPivot.DOLocalRotate(new Vector3(0f, 0f, -1.2f), 0.12f).SetEase(Ease.InOutSine));

            // Phase 3: Crisp settle into alive idle
            seq.Append(w.AnimPivot.DOAnchorPosY(0f, 0.11f).SetEase(Ease.OutSine));
            seq.Join(w.AnimPivot.DOScale(Vector3.one, 0.11f).SetEase(Ease.OutSine));
            seq.Join(w.AnimPivot.DOLocalRotate(Vector3.zero, 0.11f).SetEase(Ease.OutSine));

            seq.OnComplete(() => w.IsAnimatingTransition = false);
            w.ActiveTween = seq;
        }

        private void PlayConsumeAndHide(IntentWidget w)
        {
            w.ActiveTween?.Kill();
            w.IsVisible = false;
            w.IsAnimatingTransition = true;

            if (!w.Root.gameObject.activeSelf)
            {
                w.IsAnimatingTransition = false;
                return;
            }

            PlayBurstFlash(w, 0.75f, 1.85f, 0.24f);

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(w.AnimPivot.DOAnchorPosY(w.AnimPivot.anchoredPosition.y + 22f, 0.20f).SetEase(Ease.OutQuad));
            seq.Join(w.AnimPivot.DOScale(new Vector3(1.26f, 1.26f, 1f), 0.20f).SetEase(Ease.OutQuad));
            seq.Join(w.Group.DOFade(0f, 0.19f).SetEase(Ease.InQuad));
            seq.OnComplete(() =>
            {
                w.IsAnimatingTransition = false;
                if (!w.IsVisible && w.Root != null)
                {
                    w.Root.gameObject.SetActive(false);
                }
            });

            w.ActiveTween = seq;
        }

        private void PlayBurstFlash(IntentWidget w, float startScale, float endScale, float duration)
        {
            if (w.BurstRing == null) return;
            w.BurstTween?.Kill();

            Color c = w.Snapshot != null ? w.Snapshot.AccentColor : Color.white;
            w.BurstRing.gameObject.SetActive(true);
            w.BurstRing.color = new Color(c.r, c.g, c.b, 0.80f);
            w.BurstRing.rectTransform.localScale = Vector3.one * startScale;

            Sequence burst = DOTween.Sequence().SetUpdate(true);
            burst.Append(w.BurstRing.rectTransform.DOScale(endScale, duration).SetEase(Ease.OutCubic));
            burst.Join(w.BurstRing.DOFade(0f, duration).SetEase(Ease.OutQuad));
            burst.OnComplete(() =>
            {
                if (w.BurstRing != null) w.BurstRing.gameObject.SetActive(false);
            });
            w.BurstTween = burst;
        }

        // ------------------------------------------------------------------
        // Cursor Hover Detection & Tooltip Presentation
        // ------------------------------------------------------------------
        private void UpdateHoverTooltip()
        {
            bool suppressHover =
                (_handController != null && _handController.IsCardInteracting) ||
                (_relicSwapModal != null && _relicSwapModal.IsOpen) ||
                Time.timeScale == 0f;

            if (suppressHover)
            {
                if (_hoveredEnemy != null)
                {
                    _hoveredEnemy = null;
                    HideTooltip();
                }
                return;
            }

            Vector2 pointerScreen;
            if (_simulatedHoverScreenPos.HasValue)
            {
                pointerScreen = _simulatedHoverScreenPos.Value;
            }
            else if (Mouse.current != null)
            {
                pointerScreen = Mouse.current.position.ReadValue();
            }
            else
            {
                return;
            }

            Camera uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            EnemyView newlyHovered = null;
            IntentWidget hoveredWidget = null;

            foreach (IntentWidget w in _widgets.Values)
            {
                if (!w.IsVisible || w.Snapshot == null || w.HoverBounds == null || !w.Root.gameObject.activeSelf)
                    continue;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(w.HoverBounds, pointerScreen, uiCam, out Vector2 localHit))
                {
                    Rect r = w.HoverBounds.rect;
                    r.xMin -= _hoverPaddingPx;
                    r.xMax += _hoverPaddingPx;
                    r.yMin -= _hoverPaddingPx;
                    r.yMax += _hoverPaddingPx;

                    if (r.Contains(localHit))
                    {
                        newlyHovered = w.Enemy;
                        hoveredWidget = w;
                        break;
                    }
                }
            }

            if (newlyHovered != _hoveredEnemy)
            {
                _hoveredEnemy = newlyHovered;
                if (_hoveredEnemy != null && hoveredWidget != null)
                {
                    Tune.SFX(SfxID.hover);
                    ShowTooltip(hoveredWidget);
                }
                else
                {
                    HideTooltip();
                }
            }
            else if (_hoveredEnemy != null && hoveredWidget != null)
            {
                PositionTooltip(hoveredWidget);
            }
        }

        private void ShowTooltip(IntentWidget w)
        {
            if (_tooltipRoot == null || w.Snapshot == null) return;
            PopulateTooltip(w.Snapshot);
            _tooltipRoot.gameObject.SetActive(true);
            _tooltipRoot.SetAsLastSibling();
            PositionTooltip(w);

            _tooltipGroup.DOKill();
            _tooltipGroup.alpha = 0f;
            _tooltipGroup.DOFade(1f, 0.12f).SetUpdate(true);
        }

        private void PopulateTooltip(EnemyIntentSnapshot snap)
        {
            if (snap == null || _tooltipTitle == null) return;

            Color accent = snap.AccentColor;
            string hex = ColorUtility.ToHtmlStringRGB(accent);

            if (_tooltipAccentBar != null) _tooltipAccentBar.color = accent;
            if (_tooltipBorder != null) _tooltipBorder.color = new Color(accent.r, accent.g, accent.b, 0.85f);
            if (_tooltipEnemyName != null) _tooltipEnemyName.text = $"{snap.EnemyDisplayName}  <color=#{hex}>[{snap.ActionLabel}]</color>";
            _tooltipTitle.text = snap.TooltipTitle;
            _tooltipTitle.color = Color.Lerp(accent, Color.white, 0.30f);
            _tooltipBody.text = snap.TooltipDescription;

            float prefBodyH = _tooltipBody.GetPreferredValues(snap.TooltipDescription, 294f, 0f).y;
            float totalH = Mathf.Clamp(prefBodyH + 68f, 102f, 220f);
            _tooltipRoot.sizeDelta = new Vector2(332f, totalH);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRoot);
        }

        private void PositionTooltip(IntentWidget w)
        {
            if (_tooltipRoot == null || _canvas.transform is not RectTransform canvasRect) return;

            Vector2 anchorPos = w.Root.anchoredPosition;
            Vector2 tipSize = _tooltipRoot.sizeDelta;
            float halfW = canvasRect.rect.width * 0.5f;
            float halfH = canvasRect.rect.height * 0.5f;

            // Place cleanly above the hovered intent; if near top bar, flip cleanly to the left side
            float x = anchorPos.x;
            float y = anchorPos.y + 76f + tipSize.y * 0.5f;

            if (y + tipSize.y * 0.5f > halfH - 86f)
            {
                x = anchorPos.x - (110f + tipSize.x * 0.5f);
                y = anchorPos.y + 4f;
                if (x - tipSize.x * 0.5f < -halfW + 18f)
                {
                    x = anchorPos.x + (110f + tipSize.x * 0.5f);
                }
            }

            x = Mathf.Clamp(x, -halfW + tipSize.x * 0.5f + 16f, halfW - tipSize.x * 0.5f - 16f);
            y = Mathf.Clamp(y, -halfH + tipSize.y * 0.5f + 16f, halfH - tipSize.y * 0.5f - 86f);
            _tooltipRoot.anchoredPosition = new Vector2(x, y);
        }

        private void HideTooltip()
        {
            if (_tooltipRoot == null) return;
            _tooltipGroup?.DOKill();
            _tooltipRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Data Binding & Template Icon Lookup
        // ------------------------------------------------------------------
        private void BindSnapshot(IntentWidget w, EnemyIntentSnapshot snap, bool punchValueOnChange)
        {
            w.Snapshot = snap;
            Color accent = snap.AccentColor;

            // Color-coded top action label
            w.TopActionLabel.text = snap.ActionLabel;
            w.TopActionLabel.color = Color.Lerp(accent, Color.white, 0.14f);
            w.TopPillBorder.color = new Color(accent.r, accent.g, accent.b, 0.90f);

            // Resize top pill width cleanly to fit text
            float labelPrefW = Mathf.Clamp(w.TopActionLabel.GetPreferredValues(snap.ActionLabel).x + 22f, 84f, 176f);
            w.TopPillBorder.rectTransform.sizeDelta = new Vector2(labelPrefW, 24f);

            // Template Icon + frame tint
            Sprite icon = GetTemplateSprite(snap.Category);
            w.IconImage.sprite = icon;
            w.IconImage.enabled = icon != null;
            w.IconImage.color = Color.Lerp(accent, Color.white, 0.22f);
            w.IconBorder.color = accent;
            w.IconBg.color = new Color(accent.r * 0.16f + 0.06f, accent.g * 0.16f + 0.06f, accent.b * 0.16f + 0.08f, 0.94f);

            // Primary numeric readout (e.g. "15", "6x2", "+3", "8/t")
            bool hasVal = !string.IsNullOrEmpty(snap.ValueLabel);
            w.ValueText.gameObject.SetActive(hasVal);
            if (hasVal)
            {
                bool valueChanged = w.LastValueText != snap.ValueLabel;
                w.ValueText.text = snap.ValueLabel;
                w.ValueText.color = Color.Lerp(accent, Color.white, 0.52f);

                if (punchValueOnChange && valueChanged && !string.IsNullOrEmpty(w.LastValueText))
                {
                    w.ValuePunchTween?.Kill();
                    w.ValueText.rectTransform.localScale = Vector3.one;
                    w.ValuePunchTween = w.ValueText.rectTransform
                        .DOPunchScale(Vector3.one * 0.28f, 0.24f, 6, 0.6f)
                        .SetUpdate(true);
                }
            }
            w.LastValueText = snap.ValueLabel;

            // Optional secondary chip (e.g. "-5 WILL" on Attack + Will)
            bool hasSec = !string.IsNullOrEmpty(snap.SecondaryBadge);
            w.SecondaryChipRoot.SetActive(hasSec);
            if (hasSec)
            {
                Color sec = snap.SecondaryColor;
                w.SecondaryChipBorder.color = new Color(sec.r, sec.g, sec.b, 0.92f);
                w.SecondaryChipText.text = snap.SecondaryBadge;
                w.SecondaryChipText.color = Color.Lerp(sec, Color.white, 0.35f);
            }
        }

        private Sprite GetTemplateSprite(EnemyIntentCategory category)
        {
            Sprite picked = category switch
            {
                EnemyIntentCategory.Attack => _attackIcon,
                EnemyIntentCategory.AttackDebuff => _attackDebuffIcon != null ? _attackDebuffIcon : _attackIcon,
                EnemyIntentCategory.AttackBuff => _attackBuffIcon != null ? _attackBuffIcon : _attackIcon,
                EnemyIntentCategory.Defend => _defendIcon,
                EnemyIntentCategory.Buff => _buffIcon,
                EnemyIntentCategory.Debuff => _debuffIcon,
                EnemyIntentCategory.WillDrain => _willDrainIcon != null ? _willDrainIcon : _debuffIcon,
                EnemyIntentCategory.Heal => _healIcon,
                EnemyIntentCategory.Summon => _summonIcon != null ? _summonIcon : _buffIcon,
                EnemyIntentCategory.Stunned => _stunnedIcon,
                _ => _unknownIcon
            };

            return picked != null ? picked : _unknownIcon;
        }

        // ------------------------------------------------------------------
        // Runtime UI Construction (Non-Blocking Overlay + Hover Tooltip)
        // ------------------------------------------------------------------
        private void BuildLayersIfNeeded()
        {
            if (_canvas == null) _canvas = FindFirstObjectByType<Canvas>();
            if (_canvas == null) return;

            int layer = _canvas.gameObject.layer;

            if (_widgetsLayer == null)
            {
                Transform existing = _canvas.transform.Find("EnemyIntentLayer");
                if (existing != null)
                {
                    _widgetsLayer = (RectTransform)existing;
                }
                else
                {
                    GameObject layerGo = new("EnemyIntentLayer", typeof(RectTransform), typeof(CanvasGroup));
                    layerGo.layer = layer;
                    _widgetsLayer = (RectTransform)layerGo.transform;
                    _widgetsLayer.SetParent(_canvas.transform, false);
                    _widgetsLayer.anchorMin = Vector2.zero;
                    _widgetsLayer.anchorMax = Vector2.one;
                    _widgetsLayer.offsetMin = Vector2.zero;
                    _widgetsLayer.offsetMax = Vector2.zero;

                    CanvasGroup cg = layerGo.GetComponent<CanvasGroup>();
                    cg.blocksRaycasts = false;
                    cg.interactable = false;

                    // Keep behind HandView / CombatTopBar / Modals so cards and menus always sit cleanly above
                    _widgetsLayer.SetAsFirstSibling();
                }
            }

            if (_tooltipRoot == null)
            {
                BuildTooltip();
            }
        }

        private IntentWidget GetOrCreateWidget(EnemyView enemy)
        {
            if (_widgets.TryGetValue(enemy, out IntentWidget existing)) return existing;

            BuildLayersIfNeeded();
            Sprite solid = GetSolidSprite();
            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;
            int layer = _canvas.gameObject.layer;

            // Root (tracks enemy world position)
            GameObject rootGo = new($"Intent_{enemy.name}", typeof(RectTransform), typeof(CanvasGroup));
            rootGo.layer = layer;
            RectTransform rootRt = (RectTransform)rootGo.transform;
            rootRt.SetParent(_widgetsLayer, false);
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(180f, 114f);

            CanvasGroup cg = rootGo.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // AnimPivot (receives procedural pop-in, squash/stretch, tilt, idle float, and hover scale)
            GameObject pivotGo = new("AnimPivot", typeof(RectTransform));
            pivotGo.layer = layer;
            RectTransform pivotRt = (RectTransform)pivotGo.transform;
            pivotRt.SetParent(rootRt, false);
            pivotRt.anchorMin = pivotRt.anchorMax = new Vector2(0.5f, 0.5f);
            pivotRt.pivot = new Vector2(0.5f, 0.5f);
            pivotRt.sizeDelta = new Vector2(170f, 108f);

            // 1. Top Color-Coded Action Header Pill
            GameObject pillGo = new("TopActionPill", typeof(RectTransform), typeof(Image));
            pillGo.layer = layer;
            RectTransform pillRt = (RectTransform)pillGo.transform;
            pillRt.SetParent(pivotRt, false);
            pillRt.anchorMin = pillRt.anchorMax = new Vector2(0.5f, 1f);
            pillRt.pivot = new Vector2(0.5f, 1f);
            pillRt.anchoredPosition = new Vector2(0f, 0f);
            pillRt.sizeDelta = new Vector2(128f, 24f);
            Image pillBorder = pillGo.GetComponent<Image>();
            pillBorder.sprite = solid;
            pillBorder.raycastTarget = false;

            GameObject pillBgGo = new("Fill", typeof(RectTransform), typeof(Image));
            pillBgGo.layer = layer;
            RectTransform pillBgRt = (RectTransform)pillBgGo.transform;
            pillBgRt.SetParent(pillRt, false);
            pillBgRt.anchorMin = Vector2.zero;
            pillBgRt.anchorMax = Vector2.one;
            pillBgRt.offsetMin = new Vector2(1.5f, 2f);
            pillBgRt.offsetMax = new Vector2(-1.5f, -1.5f);
            Image pillBg = pillBgGo.GetComponent<Image>();
            pillBg.sprite = solid;
            pillBg.color = new Color(0.06f, 0.08f, 0.13f, 0.92f);
            pillBg.raycastTarget = false;

            TextMeshProUGUI topLabel = CreateText(pillBgRt, "ActionLabel", font, 12.5f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            topLabel.rectTransform.anchorMin = Vector2.zero;
            topLabel.rectTransform.anchorMax = Vector2.one;
            topLabel.rectTransform.offsetMin = new Vector2(6f, 0f);
            topLabel.rectTransform.offsetMax = new Vector2(-6f, 0f);

            // 2. Icon + Primary Value Row (centered directly below TopActionPill)
            GameObject bodyGo = new("IconRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bodyGo.layer = layer;
            RectTransform bodyRt = (RectTransform)bodyGo.transform;
            bodyRt.SetParent(pivotRt, false);
            bodyRt.anchorMin = bodyRt.anchorMax = new Vector2(0.5f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = new Vector2(0f, -29f);
            bodyRt.sizeDelta = new Vector2(160f, 54f);
            HorizontalLayoutGroup bodyHlg = bodyGo.GetComponent<HorizontalLayoutGroup>();
            bodyHlg.spacing = 7f;
            bodyHlg.childAlignment = TextAnchor.MiddleCenter;
            bodyHlg.childControlWidth = false;
            bodyHlg.childControlHeight = false;
            bodyHlg.childForceExpandWidth = false;
            bodyHlg.childForceExpandHeight = false;

            // Icon Frame (52x52 template container)
            GameObject frameGo = new("IconFrame", typeof(RectTransform), typeof(Image));
            frameGo.layer = layer;
            RectTransform frameRt = (RectTransform)frameGo.transform;
            frameRt.SetParent(bodyRt, false);
            frameRt.sizeDelta = new Vector2(52f, 52f);
            Image iconBorder = frameGo.GetComponent<Image>();
            iconBorder.sprite = solid;
            iconBorder.raycastTarget = false;

            // Procedural Burst Flash Ring behind icon frame
            GameObject burstGo = new("BurstRing", typeof(RectTransform), typeof(Image));
            burstGo.layer = layer;
            RectTransform burstRt = (RectTransform)burstGo.transform;
            burstRt.SetParent(frameRt, false);
            burstRt.anchorMin = Vector2.zero;
            burstRt.anchorMax = Vector2.one;
            burstRt.offsetMin = new Vector2(-6f, -6f);
            burstRt.offsetMax = new Vector2(6f, 6f);
            burstRt.SetAsFirstSibling();
            Image burstImg = burstGo.GetComponent<Image>();
            burstImg.sprite = solid;
            burstImg.raycastTarget = false;
            burstGo.SetActive(false);

            GameObject iconBgGo = new("Inner", typeof(RectTransform), typeof(Image));
            iconBgGo.layer = layer;
            RectTransform iconBgRt = (RectTransform)iconBgGo.transform;
            iconBgRt.SetParent(frameRt, false);
            iconBgRt.anchorMin = Vector2.zero;
            iconBgRt.anchorMax = Vector2.one;
            iconBgRt.offsetMin = new Vector2(2.5f, 2.5f);
            iconBgRt.offsetMax = new Vector2(-2.5f, -2.5f);
            Image iconBg = iconBgGo.GetComponent<Image>();
            iconBg.sprite = solid;
            iconBg.raycastTarget = false;

            GameObject iconImgGo = new("Icon", typeof(RectTransform), typeof(Image));
            iconImgGo.layer = layer;
            RectTransform iconImgRt = (RectTransform)iconImgGo.transform;
            iconImgRt.SetParent(iconBgRt, false);
            iconImgRt.anchorMin = Vector2.zero;
            iconImgRt.anchorMax = Vector2.one;
            iconImgRt.offsetMin = new Vector2(6f, 6f);
            iconImgRt.offsetMax = new Vector2(-6f, -6f);
            Image iconImg = iconImgGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Bold Value Readout beside icon
            TextMeshProUGUI valueText = CreateText(bodyRt, "ValueText", font, 28f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
            valueText.rectTransform.sizeDelta = new Vector2(68f, 46f);
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            Outline valOutline = valueText.gameObject.AddComponent<Outline>();
            valOutline.effectColor = new Color(0.03f, 0.04f, 0.07f, 0.96f);
            valOutline.effectDistance = new Vector2(2f, -2f);
            ContentSizeFitter valFitter = valueText.gameObject.AddComponent<ContentSizeFitter>();
            valFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            valFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 3. Optional Secondary Chip directly below IconRow (e.g. "-5 WILL")
            GameObject secGo = new("SecondaryChip", typeof(RectTransform), typeof(Image));
            secGo.layer = layer;
            RectTransform secRt = (RectTransform)secGo.transform;
            secRt.SetParent(pivotRt, false);
            secRt.anchorMin = secRt.anchorMax = new Vector2(0.5f, 1f);
            secRt.pivot = new Vector2(0.5f, 1f);
            secRt.anchoredPosition = new Vector2(0f, -86f);
            secRt.sizeDelta = new Vector2(82f, 20f);
            Image secBorder = secGo.GetComponent<Image>();
            secBorder.sprite = solid;
            secBorder.raycastTarget = false;

            GameObject secFillGo = new("Fill", typeof(RectTransform), typeof(Image));
            secFillGo.layer = layer;
            RectTransform secFillRt = (RectTransform)secFillGo.transform;
            secFillRt.SetParent(secRt, false);
            secFillRt.anchorMin = Vector2.zero;
            secFillRt.anchorMax = Vector2.one;
            secFillRt.offsetMin = new Vector2(1.5f, 1.5f);
            secFillRt.offsetMax = new Vector2(-1.5f, -1.5f);
            Image secFill = secFillGo.GetComponent<Image>();
            secFill.sprite = solid;
            secFill.color = new Color(0.08f, 0.06f, 0.14f, 0.92f);
            secFill.raycastTarget = false;

            TextMeshProUGUI secText = CreateText(secFillRt, "Text", font, 11.5f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            secText.rectTransform.anchorMin = Vector2.zero;
            secText.rectTransform.anchorMax = Vector2.one;
            secText.rectTransform.offsetMin = new Vector2(4f, 0f);
            secText.rectTransform.offsetMax = new Vector2(-4f, 0f);
            secGo.SetActive(false);

            rootGo.SetActive(false);

            var widget = new IntentWidget
            {
                Enemy = enemy,
                Root = rootRt,
                Group = cg,
                AnimPivot = pivotRt,
                HoverBounds = pivotRt,
                TopPillBorder = pillBorder,
                TopPillBg = pillBg,
                TopActionLabel = topLabel,
                BurstRing = burstImg,
                IconBorder = iconBorder,
                IconBg = iconBg,
                IconImage = iconImg,
                ValueText = valueText,
                SecondaryChipRoot = secGo,
                SecondaryChipBorder = secBorder,
                SecondaryChipText = secText,
                IdlePhase = (_widgets.Count * 1.35f) % (Mathf.PI * 2f)
            };

            _widgets[enemy] = widget;
            return widget;
        }

        private void BuildTooltip()
        {
            Sprite solid = GetSolidSprite();
            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;
            int layer = _canvas.gameObject.layer;

            GameObject tipGo = new("EnemyIntentTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            tipGo.layer = layer;
            _tooltipRoot = (RectTransform)tipGo.transform;
            _tooltipRoot.SetParent(_canvas.transform, false);
            _tooltipRoot.anchorMin = _tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltipRoot.pivot = new Vector2(0.5f, 0.5f);
            _tooltipRoot.sizeDelta = new Vector2(328f, 110f);

            _tooltipGroup = tipGo.GetComponent<CanvasGroup>();
            _tooltipGroup.blocksRaycasts = false;
            _tooltipGroup.interactable = false;

            _tooltipBorder = tipGo.GetComponent<Image>();
            _tooltipBorder.sprite = solid;
            _tooltipBorder.color = new Color(0.95f, 0.35f, 0.35f, 0.9f);
            _tooltipBorder.raycastTarget = false;

            GameObject bgGo = new("Background", typeof(RectTransform), typeof(Image));
            bgGo.layer = layer;
            RectTransform bgRt = (RectTransform)bgGo.transform;
            bgRt.SetParent(_tooltipRoot, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(2f, 2f);
            bgRt.offsetMax = new Vector2(-2f, -2f);
            Image bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = solid;
            bgImg.color = new Color(0.07f, 0.08f, 0.13f, 0.96f);
            bgImg.raycastTarget = false;

            // Left accent stripe
            GameObject stripeGo = new("AccentStripe", typeof(RectTransform), typeof(Image));
            stripeGo.layer = layer;
            RectTransform stripeRt = (RectTransform)stripeGo.transform;
            stripeRt.SetParent(bgRt, false);
            stripeRt.anchorMin = new Vector2(0f, 0f);
            stripeRt.anchorMax = new Vector2(0f, 1f);
            stripeRt.pivot = new Vector2(0f, 0.5f);
            stripeRt.anchoredPosition = Vector2.zero;
            stripeRt.sizeDelta = new Vector2(5f, 0f);
            _tooltipAccentBar = stripeGo.GetComponent<Image>();
            _tooltipAccentBar.sprite = solid;
            _tooltipAccentBar.raycastTarget = false;

            // Text stack
            _tooltipEnemyName = CreateText(bgRt, "EnemyName", font, 12.5f, FontStyles.Bold, new Color(0.72f, 0.77f, 0.86f, 1f), TextAlignmentOptions.Left);
            _tooltipEnemyName.rectTransform.anchorMin = new Vector2(0f, 1f);
            _tooltipEnemyName.rectTransform.anchorMax = new Vector2(1f, 1f);
            _tooltipEnemyName.rectTransform.pivot = new Vector2(0.5f, 1f);
            _tooltipEnemyName.rectTransform.offsetMin = new Vector2(16f, -26f);
            _tooltipEnemyName.rectTransform.offsetMax = new Vector2(-12f, -8f);

            _tooltipTitle = CreateText(bgRt, "IntentTitle", font, 17.5f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
            _tooltipTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _tooltipTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _tooltipTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            _tooltipTitle.rectTransform.offsetMin = new Vector2(16f, -50f);
            _tooltipTitle.rectTransform.offsetMax = new Vector2(-12f, -26f);

            _tooltipBody = CreateText(bgRt, "IntentBody", font, 14.5f, FontStyles.Normal, new Color(0.90f, 0.92f, 0.96f, 1f), TextAlignmentOptions.TopLeft);
            _tooltipBody.rectTransform.anchorMin = new Vector2(0f, 0f);
            _tooltipBody.rectTransform.anchorMax = new Vector2(1f, 1f);
            _tooltipBody.rectTransform.offsetMin = new Vector2(16f, 10f);
            _tooltipBody.rectTransform.offsetMax = new Vector2(-14f, -52f);
            _tooltipBody.textWrappingMode = TextWrappingModes.Normal;

            tipGo.SetActive(false);
        }

        private static TextMeshProUGUI CreateText(
            RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style, Color color, TextAlignmentOptions align)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            Texture2D tex = Texture2D.whiteTexture;
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _solidSprite;
        }
    }
}
