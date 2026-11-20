using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    /// <summary>
    /// Shared screen-space tooltip + template icon library for ALL Buff and Debuff icons
    /// (both Hero Portrait statuses under CombatTopBar and Enemy statuses above Enemy Intents).
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatStatusTooltipUI : MonoBehaviour
    {
        public static CombatStatusTooltipUI Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TMP_FontAsset _font;

        [Header("Status Icon Templates (Swap with Custom Art Anytime)")]
        [SerializeField] private Sprite _buffIcon;
        [SerializeField] private Sprite _attackIcon;
        [SerializeField] private Sprite _defendIcon;
        [SerializeField] private Sprite _weakIcon;
        [SerializeField] private Sprite _vulnerableIcon;
        [SerializeField] private Sprite _poisonIcon;
        [SerializeField] private Sprite _burnIcon;
        [SerializeField] private Sprite _thornsIcon;
        [SerializeField] private Sprite _stunIcon;
        [SerializeField] private Sprite _willPositiveIcon;
        [SerializeField] private Sprite _willFearIcon;
        [SerializeField] private Sprite _intangibleIcon;
        [SerializeField] private Sprite _unknownIcon;

        private static Sprite _solidSprite;

        private RectTransform _tooltipRoot;
        private CanvasGroup _tooltipGroup;
        private Image _border;
        private Image _accentBar;
        private TextMeshProUGUI _headerTag;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _bodyText;
        private TextMeshProUGUI _footerText;

        private object _currentOwner;
        private string _currentBadgeId;

        public bool IsVisible => _tooltipRoot != null && _tooltipRoot.gameObject.activeSelf;
        public string CurrentTitle => _titleText != null ? _titleText.text : string.Empty;
        public TMP_FontAsset Font => _font != null ? _font : TMP_Settings.defaultFontAsset;

        private void Awake()
        {
            Instance = this;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_canvas == null) _canvas = FindFirstObjectByType<Canvas>();
            BuildTooltipIfNeeded();
        }

        private void OnEnable()
        {
            Instance = this;
            BuildTooltipIfNeeded();
        }

        private void OnDisable()
        {
            Hide(_currentOwner);
            if (Instance == this) Instance = null;
        }

        public Sprite GetSprite(StatusIconKey key)
        {
            Sprite picked = key switch
            {
                StatusIconKey.Buff => _buffIcon,
                StatusIconKey.Attack => _attackIcon,
                StatusIconKey.Defend => _defendIcon,
                StatusIconKey.Weak => _weakIcon,
                StatusIconKey.Vulnerable => _vulnerableIcon,
                StatusIconKey.Poison => _poisonIcon,
                StatusIconKey.Burn => _burnIcon,
                StatusIconKey.Thorns => _thornsIcon,
                StatusIconKey.Stun => _stunIcon,
                StatusIconKey.WillPositive => _willPositiveIcon != null ? _willPositiveIcon : _buffIcon,
                StatusIconKey.WillFear => _willFearIcon,
                StatusIconKey.Intangible => _intangibleIcon != null ? _intangibleIcon : _buffIcon,
                _ => _unknownIcon
            };
            return picked != null ? picked : _unknownIcon;
        }

        public void Show(CombatStatusBadgeData badge, RectTransform anchorRect, object owner, bool preferBelow = false, bool playHoverSfx = true)
        {
            if (badge == null || anchorRect == null) return;
            BuildTooltipIfNeeded();
            if (_tooltipRoot == null) return;

            bool isNew = !IsVisible || _currentOwner != owner || _currentBadgeId != badge.Id;
            _currentOwner = owner;
            _currentBadgeId = badge.Id;

            Color accent = badge.AccentColor;
            string hex = ColorUtility.ToHtmlStringRGB(accent);
            string kindTag = badge.IsDebuff ? "DEBUFF" : "BUFF";

            _border.color = new Color(accent.r, accent.g, accent.b, 0.88f);
            _accentBar.color = accent;
            _headerTag.text = $"<color=#{hex}>[{kindTag}]</color>";
            _titleText.text = string.IsNullOrEmpty(badge.StackLabel)
                ? badge.Title
                : $"{badge.Title}  <color=#{hex}>×{badge.StackLabel}</color>";
            _titleText.color = Color.Lerp(accent, Color.white, 0.35f);
            _bodyText.text = badge.Description;

            string stackSummary = !string.IsNullOrEmpty(badge.StackLabel)
                ? $"Stacks: {badge.StackLabel}"
                : string.Empty;
            string footerCombined = !string.IsNullOrEmpty(stackSummary) && !string.IsNullOrEmpty(badge.DurationFooter) &&
                                    !badge.DurationFooter.Contains("stack")
                ? $"{stackSummary}  •  {badge.DurationFooter}"
                : (!string.IsNullOrEmpty(badge.DurationFooter) ? badge.DurationFooter : stackSummary);

            bool hasFooter = !string.IsNullOrEmpty(footerCombined);
            _footerText.gameObject.SetActive(hasFooter);
            if (hasFooter) _footerText.text = footerCombined;

            float bodyH = _bodyText.GetPreferredValues(badge.Description, 272f, 0f).y;
            float totalH = Mathf.Clamp(bodyH + (hasFooter ? 78f : 58f), 96f, 210f);
            _tooltipRoot.sizeDelta = new Vector2(310f, totalH);

            _tooltipRoot.gameObject.SetActive(true);
            _tooltipRoot.SetAsLastSibling();
            PositionRelativeTo(anchorRect, preferBelow);

            if (isNew)
            {
                if (playHoverSfx) Tune.SFX(SfxID.hover);
                _tooltipGroup.DOKill();
                _tooltipGroup.alpha = 0f;
                _tooltipGroup.DOFade(1f, 0.11f).SetUpdate(true);
            }
        }

        public void Hide(object owner)
        {
            if (_tooltipRoot == null) return;
            if (owner != null && _currentOwner != null && _currentOwner != owner) return;

            _currentOwner = null;
            _currentBadgeId = null;
            _tooltipGroup?.DOKill();
            _tooltipRoot.gameObject.SetActive(false);
        }

        private void PositionRelativeTo(RectTransform anchorRect, bool preferBelow)
        {
            if (_canvas == null || _canvas.transform is not RectTransform canvasRect) return;
            Camera uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(uiCam, anchorRect.TransformPoint(anchorRect.rect.center));
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPt, uiCam, out Vector2 localAnchor))
                return;

            Vector2 size = _tooltipRoot.sizeDelta;
            float halfW = canvasRect.rect.width * 0.5f;
            float halfH = canvasRect.rect.height * 0.5f;

            float offsetY = preferBelow
                ? -(anchorRect.rect.height * 0.5f + size.y * 0.5f + 12f)
                : (anchorRect.rect.height * 0.5f + size.y * 0.5f + 14f);

            float x = localAnchor.x;
            float y = localAnchor.y + offsetY;

            // If placing above would clip into the Top Bar, flip cleanly to the left or below
            if (!preferBelow && y + size.y * 0.5f > halfH - 86f)
            {
                x = localAnchor.x - (size.x * 0.5f + 28f);
                y = localAnchor.y - 6f;
                if (x - size.x * 0.5f < -halfW + 16f)
                {
                    x = localAnchor.x + (size.x * 0.5f + 28f);
                }
            }

            x = Mathf.Clamp(x, -halfW + size.x * 0.5f + 16f, halfW - size.x * 0.5f - 16f);
            y = Mathf.Clamp(y, -halfH + size.y * 0.5f + 16f, halfH - size.y * 0.5f - 84f);
            _tooltipRoot.anchoredPosition = new Vector2(x, y);
        }

        private void BuildTooltipIfNeeded()
        {
            if (_tooltipRoot != null || _canvas == null) return;

            Sprite solid = GetSolidSprite();
            TMP_FontAsset font = Font;
            int layer = _canvas.gameObject.layer;

            GameObject go = new("CombatStatusTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.layer = layer;
            _tooltipRoot = (RectTransform)go.transform;
            _tooltipRoot.SetParent(_canvas.transform, false);
            _tooltipRoot.anchorMin = _tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltipRoot.pivot = new Vector2(0.5f, 0.5f);
            _tooltipRoot.sizeDelta = new Vector2(310f, 108f);

            _tooltipGroup = go.GetComponent<CanvasGroup>();
            _tooltipGroup.blocksRaycasts = false;
            _tooltipGroup.interactable = false;

            _border = go.GetComponent<Image>();
            _border.sprite = solid;
            _border.raycastTarget = false;

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
            bgImg.color = new Color(0.06f, 0.08f, 0.13f, 0.97f);
            bgImg.raycastTarget = false;

            GameObject stripeGo = new("AccentStripe", typeof(RectTransform), typeof(Image));
            stripeGo.layer = layer;
            RectTransform stripeRt = (RectTransform)stripeGo.transform;
            stripeRt.SetParent(bgRt, false);
            stripeRt.anchorMin = new Vector2(0f, 0f);
            stripeRt.anchorMax = new Vector2(0f, 1f);
            stripeRt.pivot = new Vector2(0f, 0.5f);
            stripeRt.sizeDelta = new Vector2(5f, 0f);
            _accentBar = stripeGo.GetComponent<Image>();
            _accentBar.sprite = solid;
            _accentBar.raycastTarget = false;

            _headerTag = CreateText(bgRt, "KindTag", font, 11.5f, FontStyles.Bold, Color.white, TextAlignmentOptions.Right);
            _headerTag.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _headerTag.rectTransform.anchorMax = new Vector2(1f, 1f);
            _headerTag.rectTransform.pivot = new Vector2(1f, 1f);
            _headerTag.rectTransform.offsetMin = new Vector2(0f, -26f);
            _headerTag.rectTransform.offsetMax = new Vector2(-12f, -8f);

            _titleText = CreateText(bgRt, "Title", font, 16.5f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
            _titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _titleText.rectTransform.offsetMin = new Vector2(15f, -34f);
            _titleText.rectTransform.offsetMax = new Vector2(-70f, -8f);

            _bodyText = CreateText(bgRt, "Body", font, 14f, FontStyles.Normal, new Color(0.90f, 0.93f, 0.97f, 1f), TextAlignmentOptions.TopLeft);
            _bodyText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _bodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _bodyText.rectTransform.offsetMin = new Vector2(15f, 24f);
            _bodyText.rectTransform.offsetMax = new Vector2(-14f, -36f);
            _bodyText.textWrappingMode = TextWrappingModes.Normal;

            _footerText = CreateText(bgRt, "Footer", font, 12f, FontStyles.Italic, new Color(0.68f, 0.74f, 0.84f, 1f), TextAlignmentOptions.BottomLeft);
            _footerText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _footerText.rectTransform.anchorMax = new Vector2(1f, 0f);
            _footerText.rectTransform.pivot = new Vector2(0.5f, 0f);
            _footerText.rectTransform.offsetMin = new Vector2(15f, 6f);
            _footerText.rectTransform.offsetMax = new Vector2(-12f, 24f);

            go.SetActive(false);
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

        public static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            Texture2D tex = Texture2D.whiteTexture;
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _solidSprite;
        }
    }
}

