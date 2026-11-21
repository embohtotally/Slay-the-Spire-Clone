using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    public enum CombatPileKind
    {
        Draw,
        Discard,
        Exhaust
    }

    /// <summary>
    /// Clickable in-combat modal browser for Discard Pile, Draw Pile (sorted to avoid revealing draw order),
    /// and Exhaust Pile — styled with existing project UI assets (Panel.png, panel_name_box.png, Tombol.png,
    /// and C_Run Deck Card View.prefab).
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatPileViewerUI : MonoBehaviour
    {
        public static bool IsAnyOpen { get; private set; }

        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RunDeckCardView _cardViewPrefab;
        [SerializeField] private TMP_FontAsset _font;

        [Header("Existing Project Art Assets")]
        [SerializeField] private Sprite _panelSprite;
        [SerializeField] private Sprite _headerBannerSprite;
        [SerializeField] private Sprite _buttonSprite;

        private static Sprite _solidSprite;

        private GameObject _modalRoot;
        private CanvasGroup _modalGroup;
        private RectTransform _panelRect;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _subtitleText;
        private TextMeshProUGUI _emptyText;
        private Transform _gridContent;
        private ScrollRect _scrollRect;
        private Image _drawTabImg;
        private Image _discardTabImg;
        private Image _exhaustTabImg;
        private TextMeshProUGUI _drawTabLabel;
        private TextMeshProUGUI _discardTabLabel;
        private TextMeshProUGUI _exhaustTabLabel;

        private readonly List<RunDeckCardView> _spawnedViews = new();

        private CombatPileKind _activePile = CombatPileKind.Discard;
        private bool _isOpen;
        private CardSystem _subscribedCardSystem;

        public bool IsOpen => _isOpen;
        public CombatPileKind ActivePile => _activePile;
        public int VisibleCardCount { get; private set; }

        private void Awake()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_canvas == null) _canvas = FindFirstObjectByType<Canvas>();
            EnsurePileButtonsClickable();
        }

        private void Start()
        {
            EnsurePileButtonsClickable();
        }

        private void OnEnable()
        {
            TrySubscribeCardSystem();
        }

        private void OnDisable()
        {
            if (_isOpen)
            {
                _isOpen = false;
                IsAnyOpen = false;
            }

            if (_subscribedCardSystem != null)
            {
                _subscribedCardSystem.PilesChanged -= OnPilesChanged;
                _subscribedCardSystem = null;
            }
        }

        private void Update()
        {
            if (_subscribedCardSystem == null && CardSystem.Instance != null)
            {
                TrySubscribeCardSystem();
            }

            if (_isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        public void OpenDrawPile() => OpenPile(CombatPileKind.Draw);
        public void OpenDiscardPile() => OpenPile(CombatPileKind.Discard);
        public void OpenExhaustPile() => OpenPile(CombatPileKind.Exhaust);

        public void ToggleDrawPile()
        {
            if (_isOpen && _activePile == CombatPileKind.Draw) Close();
            else OpenPile(CombatPileKind.Draw);
        }

        public void ToggleDiscardPile()
        {
            if (_isOpen && _activePile == CombatPileKind.Discard) Close();
            else OpenPile(CombatPileKind.Discard);
        }

        public void ToggleExhaustPile()
        {
            if (_isOpen && _activePile == CombatPileKind.Exhaust) Close();
            else OpenPile(CombatPileKind.Exhaust);
        }

        public void OpenPile(CombatPileKind kind)
        {
            BuildModalIfNeeded();
            if (_modalRoot == null) return;

            bool wasOpen = _isOpen;
            _activePile = kind;
            _isOpen = true;
            IsAnyOpen = true;

            _modalRoot.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();
            RefreshView(animateCards: true);

            if (_scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                _scrollRect.verticalNormalizedPosition = 1f;
            }

            if (!wasOpen)
            {
                PlayOpenAnimation();
                Tune.SFX(SfxID.DrawCard);
            }
            else
            {
                Tune.SFX(SfxID.hover);
            }
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            IsAnyOpen = false;
            Tune.SFX(SfxID.ui_cancel);

            if (_modalRoot != null)
            {
                _modalGroup?.DOKill();
                _panelRect?.DOKill();
                _modalRoot.SetActive(false);
            }
        }

        private void PlayOpenAnimation()
        {
            if (_modalGroup == null || _panelRect == null) return;
            _modalGroup.DOKill();
            _panelRect.DOKill();
            _modalGroup.alpha = 0f;
            _panelRect.localScale = Vector3.one * 0.88f;
            _modalGroup.DOFade(1f, 0.16f).SetUpdate(true);
            _panelRect.DOScale(Vector3.one, 0.24f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void OnPilesChanged()
        {
            if (_isOpen) RefreshView(animateCards: false);
        }

        private void TrySubscribeCardSystem()
        {
            if (CardSystem.Instance == null || _subscribedCardSystem == CardSystem.Instance) return;
            _subscribedCardSystem = CardSystem.Instance;
            _subscribedCardSystem.PilesChanged += OnPilesChanged;
        }

        /// <summary>
        /// Ensures both the Discard Pile and Draw Pile buttons in the scene have raycastTarget enabled
        /// and receive juicy hover scale feedback so clicks are never missed.
        /// </summary>
        private void EnsurePileButtonsClickable()
        {
            if (_canvas == null) return;
            Button[] buttons = _canvas.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                if (btn == null) continue;
                int count = btn.onClick.GetPersistentEventCount();
                bool wiredToThis = false;
                for (int i = 0; i < count; i++)
                {
                    if (btn.onClick.GetPersistentTarget(i) == this)
                    {
                        wiredToThis = true;
                        break;
                    }
                }

                if (!wiredToThis) continue;

                if (btn.targetGraphic != null)
                {
                    btn.targetGraphic.raycastTarget = true;
                }

                if (btn.GetComponent<PileButtonHoverJuice>() == null)
                {
                    btn.gameObject.AddComponent<PileButtonHoverJuice>();
                }
            }
        }

        private void RefreshView(bool animateCards = false)
        {
            if (_modalRoot == null) return;

            IReadOnlyList<Card> drawSrc = GetPileSource(CombatPileKind.Draw);
            IReadOnlyList<Card> discardSrc = GetPileSource(CombatPileKind.Discard);
            IReadOnlyList<Card> exhaustSrc = GetPileSource(CombatPileKind.Exhaust);
            IReadOnlyList<Card> source = GetPileSource(_activePile);

            UpdateTabVisuals(drawSrc.Count, discardSrc.Count, exhaustSrc.Count);

            string pileLabel = _activePile switch
            {
                CombatPileKind.Draw => "DRAW PILE",
                CombatPileKind.Discard => "DISCARD PILE",
                _ => "EXHAUST PILE"
            };
            string cardWord = source.Count == 1 ? "card" : "cards";
            string subtitle = _activePile switch
            {
                CombatPileKind.Draw => $"{source.Count} {cardWord} remaining  •  Sorted alphabetically (draw order is shuffled)",
                CombatPileKind.Discard => $"{source.Count} {cardWord} in discard pile  •  Most recently discarded shown first",
                _ => $"{source.Count} {cardWord} exhausted this combat"
            };

            _titleText.text = pileLabel;
            _subtitleText.text = subtitle;

            List<Card> displayList = _activePile switch
            {
                CombatPileKind.Draw => source.Where(c => c != null).OrderBy(c => c.Mana).ThenBy(c => c.Title).ToList(),
                CombatPileKind.Discard => source.Where(c => c != null).Reverse().ToList(),
                _ => source.Where(c => c != null).ToList()
            };

            VisibleCardCount = displayList.Count;
            bool isEmpty = displayList.Count == 0;
            _emptyText.gameObject.SetActive(isEmpty);
            if (isEmpty)
            {
                _emptyText.text = _activePile == CombatPileKind.Discard
                    ? "Your discard pile is empty.\nPlayed or discarded cards will appear here."
                    : $"No cards in {pileLabel.ToLowerInvariant()}.";
            }

            EnsureCardViews(displayList.Count);
            for (int i = 0; i < _spawnedViews.Count; i++)
            {
                RunDeckCardView view = _spawnedViews[i];
                if (view == null) continue;
                if (i < displayList.Count)
                {
                    view.gameObject.SetActive(true);
                    Card card = displayList[i];
                    if (card.SourceData != null)
                        view.Setup(card.SourceData, 1, i);
                    else
                        view.Setup(card);

                    view.transform.DOKill();
                    if (animateCards)
                    {
                        view.transform.localScale = Vector3.one * 0.80f;
                        float delay = Mathf.Min(i, 12) * 0.024f;
                        view.transform.DOScale(Vector3.one, 0.20f)
                            .SetDelay(delay)
                            .SetEase(Ease.OutBack)
                            .SetUpdate(true);
                    }
                    else
                    {
                        view.transform.localScale = Vector3.one;
                    }
                }
                else
                {
                    view.transform.DOKill();
                    view.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateTabVisuals(int drawCount, int discardCount, int exhaustCount)
        {
            if (_drawTabLabel != null) _drawTabLabel.text = $"Draw ({drawCount})";
            if (_discardTabLabel != null) _discardTabLabel.text = $"Discard ({discardCount})";
            if (_exhaustTabLabel != null) _exhaustTabLabel.text = $"Exhaust ({exhaustCount})";

            Color activeColor = _buttonSprite != null
                ? new Color(1f, 0.96f, 0.82f, 1f)
                : new Color(0.84f, 0.66f, 0.30f, 1f);
            Color inactiveColor = _buttonSprite != null
                ? new Color(0.72f, 0.63f, 0.50f, 0.88f)
                : new Color(0.24f, 0.20f, 0.16f, 0.95f);

            if (_drawTabImg != null) _drawTabImg.color = _activePile == CombatPileKind.Draw ? activeColor : inactiveColor;
            if (_discardTabImg != null) _discardTabImg.color = _activePile == CombatPileKind.Discard ? activeColor : inactiveColor;
            if (_exhaustTabImg != null) _exhaustTabImg.color = _activePile == CombatPileKind.Exhaust ? activeColor : inactiveColor;
        }

        private static IReadOnlyList<Card> GetPileSource(CombatPileKind kind)
        {
            if (CardSystem.Instance == null) return System.Array.Empty<Card>();
            return kind switch
            {
                CombatPileKind.Draw => CardSystem.Instance.DrawPile,
                CombatPileKind.Discard => CardSystem.Instance.DiscardPile,
                _ => CardSystem.Instance.ExhaustPile
            };
        }

        private void EnsureCardViews(int targetCount)
        {
            if (_cardViewPrefab == null || _gridContent == null) return;
            while (_spawnedViews.Count < targetCount)
            {
                RunDeckCardView view = Instantiate(_cardViewPrefab, _gridContent);
                view.transform.localScale = Vector3.one;
                _spawnedViews.Add(view);
            }
        }

        private void BuildModalIfNeeded()
        {
            if (_modalRoot != null) return;
            if (_canvas == null) _canvas = FindFirstObjectByType<Canvas>();
            if (_canvas == null) return;

            Sprite solid = GetSolidSprite();
            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;
            int layer = _canvas.gameObject.layer;

            // Fullscreen dim backdrop (clicking outside panel closes the viewer)
            _modalRoot = new GameObject("CombatPileViewerModal", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
            _modalRoot.layer = layer;
            RectTransform rootRect = (RectTransform)_modalRoot.transform;
            rootRect.SetParent(_canvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            _modalGroup = _modalRoot.GetComponent<CanvasGroup>();
            Image dim = _modalRoot.GetComponent<Image>();
            dim.sprite = solid;
            dim.color = new Color(0.03f, 0.02f, 0.01f, 0.80f);
            Button dimBtn = _modalRoot.GetComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Close);

            // Framed modal board using existing project Panel.png
            GameObject panelGo = new("Panel", typeof(RectTransform), typeof(Image));
            panelGo.layer = layer;
            _panelRect = (RectTransform)panelGo.transform;
            _panelRect.SetParent(rootRect, false);
            _panelRect.anchorMin = new Vector2(0.06f, 0.07f);
            _panelRect.anchorMax = new Vector2(0.94f, 0.93f);
            _panelRect.offsetMin = Vector2.zero;
            _panelRect.offsetMax = Vector2.zero;

            Image panelBg = panelGo.GetComponent<Image>();
            if (_panelSprite != null)
            {
                panelBg.sprite = _panelSprite;
                panelBg.color = Color.white;
            }
            else
            {
                panelBg.sprite = solid;
                panelBg.color = new Color(0.18f, 0.11f, 0.06f, 0.97f);
            }
            panelBg.raycastTarget = true; // Absorb clicks inside panel

            // Ornate Title Ribbon using existing project panel_name_box.png
            GameObject bannerGo = new("TitleBanner", typeof(RectTransform), typeof(Image));
            bannerGo.layer = layer;
            RectTransform bannerRect = (RectTransform)bannerGo.transform;
            bannerRect.SetParent(_panelRect, false);
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0f, 1f);
            bannerRect.pivot = new Vector2(0f, 1f);
            bannerRect.anchoredPosition = new Vector2(42f, -24f);
            bannerRect.sizeDelta = new Vector2(360f, 62f);

            Image bannerImg = bannerGo.GetComponent<Image>();
            if (_headerBannerSprite != null)
            {
                bannerImg.sprite = _headerBannerSprite;
                bannerImg.color = Color.white;
            }
            else
            {
                bannerImg.sprite = solid;
                bannerImg.color = new Color(0.84f, 0.72f, 0.48f, 1f);
            }
            bannerImg.raycastTarget = false;

            _titleText = CreateLabel(bannerRect, "Title", font, 28f, FontStyles.Bold, new Color(0.20f, 0.12f, 0.05f, 1f),
                Vector2.zero, Vector2.one, new Vector2(28f, 4f), new Vector2(-28f, -4f));
            _titleText.alignment = TextAlignmentOptions.Center;

            // Subtitle below the banner
            _subtitleText = CreateLabel(_panelRect, "Subtitle", font, 18f, FontStyles.Bold, new Color(0.93f, 0.85f, 0.68f, 0.95f),
                new Vector2(0f, 1f), new Vector2(0.82f, 1f), new Vector2(48f, -120f), new Vector2(-48f, -88f));
            _subtitleText.alignment = TextAlignmentOptions.Left;

            // Pile Tabs + Close Button styled with existing Tombol.png
            Sprite btnSprite = _buttonSprite != null ? _buttonSprite : solid;
            Color btnTextColor = _buttonSprite != null ? new Color(0.20f, 0.12f, 0.05f, 1f) : Color.white;

            (_discardTabImg, _discardTabLabel) = CreateTabButton(
                _panelRect, "DiscardTab", "Discard (0)", font, btnSprite, btnTextColor,
                new Vector2(-468f, -55f), new Vector2(148f, 48f), () => OpenPile(CombatPileKind.Discard));

            (_drawTabImg, _drawTabLabel) = CreateTabButton(
                _panelRect, "DrawTab", "Draw (0)", font, btnSprite, btnTextColor,
                new Vector2(-310f, -55f), new Vector2(144f, 48f), () => OpenPile(CombatPileKind.Draw));

            (_exhaustTabImg, _exhaustTabLabel) = CreateTabButton(
                _panelRect, "ExhaustTab", "Exhaust (0)", font, btnSprite, btnTextColor,
                new Vector2(-154f, -55f), new Vector2(148f, 48f), () => OpenPile(CombatPileKind.Exhaust));

            CreateTabButton(
                _panelRect, "CloseBtn", "X", font, btnSprite, new Color(0.55f, 0.10f, 0.08f, 1f),
                new Vector2(-48f, -55f), new Vector2(54f, 48f), Close,
                _buttonSprite != null ? new Color(1f, 0.88f, 0.82f, 1f) : new Color(0.68f, 0.22f, 0.22f, 1f));

            // Recessed ScrollView area inside the framed panel
            GameObject scrollGo = new("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.layer = layer;
            RectTransform scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.SetParent(_panelRect, false);
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(44f, 42f);
            scrollRect.offsetMax = new Vector2(-62f, -128f);

            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.sprite = solid;
            scrollBg.color = new Color(0.08f, 0.05f, 0.03f, 0.52f);

            GameObject viewportGo = new("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.layer = layer;
            RectTransform viewportRect = (RectTransform)viewportGo.transform;
            viewportRect.SetParent(scrollRect, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            Image vpImg = viewportGo.GetComponent<Image>();
            vpImg.sprite = solid;
            vpImg.color = Color.clear;

            GameObject contentGo = new("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.layer = layer;
            RectTransform contentRect = (RectTransform)contentGo.transform;
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            GridLayoutGroup grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(270f, 334f);
            grid.spacing = new Vector2(38f, 28f);
            grid.padding = new RectOffset(36, 36, 26, 28);
            grid.childAlignment = TextAnchor.UpperLeft;

            ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Vertical Scrollbar tucked on the right edge of the ScrollView
            Scrollbar verticalBar = CreateScrollbar(_panelRect, solid, layer);

            _scrollRect = scrollGo.GetComponent<ScrollRect>();
            _scrollRect.viewport = viewportRect;
            _scrollRect.content = contentRect;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Elastic;
            _scrollRect.scrollSensitivity = 42f;
            _scrollRect.verticalScrollbar = verticalBar;
            _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            _gridContent = contentRect;

            _emptyText = CreateLabel(scrollRect, "EmptyText", font, 26f, FontStyles.Italic, new Color(0.90f, 0.82f, 0.66f, 0.90f),
                Vector2.zero, Vector2.one, new Vector2(40f, 40f), new Vector2(-40f, -40f));
            _emptyText.alignment = TextAlignmentOptions.Center;

            _modalRoot.SetActive(false);
        }

        private static Scrollbar CreateScrollbar(RectTransform panelRect, Sprite solid, int layer)
        {
            GameObject sbGo = new("ScrollbarVertical", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            sbGo.layer = layer;
            RectTransform sbRt = (RectTransform)sbGo.transform;
            sbRt.SetParent(panelRect, false);
            sbRt.anchorMin = new Vector2(1f, 0f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.offsetMin = new Vector2(-54f, 44f);
            sbRt.offsetMax = new Vector2(-40f, -130f);

            Image trackImg = sbGo.GetComponent<Image>();
            trackImg.sprite = solid;
            trackImg.color = new Color(0.10f, 0.06f, 0.03f, 0.72f);

            GameObject slideArea = new("SlidingArea", typeof(RectTransform));
            slideArea.layer = layer;
            RectTransform slideRt = (RectTransform)slideArea.transform;
            slideRt.SetParent(sbRt, false);
            slideRt.anchorMin = Vector2.zero;
            slideRt.anchorMax = Vector2.one;
            slideRt.offsetMin = new Vector2(2f, 4f);
            slideRt.offsetMax = new Vector2(-2f, -4f);

            GameObject handleGo = new("Handle", typeof(RectTransform), typeof(Image));
            handleGo.layer = layer;
            RectTransform handleRt = (RectTransform)handleGo.transform;
            handleRt.SetParent(slideRt, false);
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            Image handleImg = handleGo.GetComponent<Image>();
            handleImg.sprite = solid;
            handleImg.color = new Color(0.84f, 0.67f, 0.36f, 0.92f);

            Scrollbar sb = sbGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.targetGraphic = handleImg;
            sb.handleRect = handleRt;
            return sb;
        }

        private static TextMeshProUGUI CreateLabel(
            RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            TextMeshProUGUI txt = go.GetComponent<TextMeshProUGUI>();
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.raycastTarget = false;
            if (font != null) txt.font = font;
            return txt;
        }

        private static (Image, TextMeshProUGUI) CreateTabButton(
            RectTransform parent, string name, string label, TMP_FontAsset font, Sprite sprite, Color textColor,
            Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick, Color? bgColor = null)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = bgColor ?? Color.white;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            TextMeshProUGUI txt = CreateLabel(rect, "Text", font, 18f, FontStyles.Bold, textColor,
                Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));
            txt.alignment = TextAlignmentOptions.Center;
            txt.text = label;
            return (img, txt);
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            Texture2D tex = Texture2D.whiteTexture;
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _solidSprite;
        }

        private sealed class PileButtonHoverJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private Vector3 _baseScale = Vector3.one;
            private bool _capturedBaseScale;

            private void Awake()
            {
                TryCaptureBaseScale();
            }

            private void TryCaptureBaseScale()
            {
                var director = PlushieOdyssey.Vfx.CombatSceneEntranceDirector.Instance;
                if (director != null && director.TryGetStagedHomeScale(transform, out Vector3 homeScale))
                {
                    _baseScale = homeScale;
                    _capturedBaseScale = true;
                    return;
                }

                // Never capture temporary 0.45x scale while the battle entrance animation is staging!
                if (director != null && director.IsStagingEntrance)
                {
                    return;
                }

                if (!_capturedBaseScale && transform.localScale.sqrMagnitude > 0.001f)
                {
                    _baseScale = transform.localScale;
                    _capturedBaseScale = true;
                }
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                var director = PlushieOdyssey.Vfx.CombatSceneEntranceDirector.Instance;
                if (director != null && director.IsStagingEntrance) return;

                if (!_capturedBaseScale) TryCaptureBaseScale();
                if (!_capturedBaseScale) _baseScale = Vector3.one;

                transform.DOKill();
                transform.DOScale(_baseScale * 1.10f, 0.12f).SetEase(Ease.OutBack).SetUpdate(true);
                Tune.SFX(SfxID.hover);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                var director = PlushieOdyssey.Vfx.CombatSceneEntranceDirector.Instance;
                if (director != null && director.IsStagingEntrance) return;

                if (!_capturedBaseScale) TryCaptureBaseScale();
                if (!_capturedBaseScale) _baseScale = Vector3.one;

                transform.DOKill();
                transform.DOScale(_baseScale, 0.12f).SetEase(Ease.OutQuad).SetUpdate(true);
            }

            private void OnDisable()
            {
                transform.DOKill();
                if (_capturedBaseScale)
                {
                    transform.localScale = _baseScale;
                }
            }
        }
    }
}
