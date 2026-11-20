using System;
using System.Collections.Generic;
using System.Linq;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Data;
using PlushieOdyssey.Inputs;
using TMPro;
using UnityEngine;
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
    /// Clickable in-combat modal browser for Draw Pile (sorted to avoid revealing draw order),
    /// Discard Pile, and Exhaust Pile.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatPileViewerUI : MonoBehaviour, IBackHandler
    {
        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RunDeckCardView _cardViewPrefab;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private InputContextRouter _inputRouter;

        private static Sprite _solidSprite;

        private GameObject _modalRoot;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _subtitleText;
        private TextMeshProUGUI _emptyText;
        private Transform _gridContent;
        private readonly List<RunDeckCardView> _spawnedViews = new();

        private CombatPileKind _activePile = CombatPileKind.Draw;
        private bool _isOpen;
        private CardSystem _subscribedCardSystem;
        private IDisposable _contextLease;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }

        private void OnEnable()
        {
            TrySubscribeCardSystem();
        }

        private void OnDisable()
        {
            Close();
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
            if (!isActiveAndEnabled) return;
            BuildModalIfNeeded();
            if (_modalRoot == null) return;

            _activePile = kind;
            _isOpen = true;
            if (_inputRouter == null) _inputRouter = InputContextRouter.FromBootstrap;
            if (_contextLease == null && _inputRouter != null && _inputRouter.isActiveAndEnabled)
                _contextLease = _inputRouter.EnterContext(this, InputContext.Modal, this);
            _modalRoot.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();
            RefreshView();
        }

        public void Close()
        {
            _isOpen = false;
            _contextLease?.Dispose();
            _contextLease = null;
            if (_modalRoot != null) _modalRoot.SetActive(false);
        }

        public bool TryHandleBack()
        {
            if (!_isOpen) return false;
            Close();
            return true;
        }

        private void OnPilesChanged()
        {
            if (_isOpen) RefreshView();
        }

        private void TrySubscribeCardSystem()
        {
            if (CardSystem.Instance == null || _subscribedCardSystem == CardSystem.Instance) return;
            _subscribedCardSystem = CardSystem.Instance;
            _subscribedCardSystem.PilesChanged += OnPilesChanged;
        }

        private void RefreshView()
        {
            if (_modalRoot == null) return;

            IReadOnlyList<Card> source = GetPileSource(_activePile);
            string pileLabel = _activePile switch
            {
                CombatPileKind.Draw => "DRAW PILE",
                CombatPileKind.Discard => "DISCARD PILE",
                _ => "EXHAUST PILE"
            };
            string subtitle = _activePile switch
            {
                CombatPileKind.Draw => $"{source.Count} cards remaining (sorted alphabetically — actual draw order is random)",
                CombatPileKind.Discard => $"{source.Count} cards in discard pile",
                _ => $"{source.Count} cards exhausted this combat"
            };

            _titleText.text = pileLabel;
            _subtitleText.text = subtitle;

            List<Card> displayList = _activePile == CombatPileKind.Draw
                ? source.Where(c => c != null).OrderBy(c => c.Mana).ThenBy(c => c.Title).ToList()
                : source.Where(c => c != null).ToList();

            bool isEmpty = displayList.Count == 0;
            _emptyText.gameObject.SetActive(isEmpty);
            if (isEmpty) _emptyText.text = $"No cards in {pileLabel.ToLowerInvariant()}.";

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
                }
                else
                {
                    view.gameObject.SetActive(false);
                }
            }
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

            _modalRoot = new GameObject("CombatPileViewerModal", typeof(RectTransform), typeof(Image), typeof(Button));
            _modalRoot.layer = _canvas.gameObject.layer;
            RectTransform rootRect = (RectTransform)_modalRoot.transform;
            rootRect.SetParent(_canvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image dim = _modalRoot.GetComponent<Image>();
            dim.sprite = solid;
            dim.color = new Color(0.03f, 0.04f, 0.07f, 0.82f);
            Button dimBtn = _modalRoot.GetComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Close);

            // Panel Box
            GameObject panelGo = new("Panel", typeof(RectTransform), typeof(Image));
            panelGo.layer = _modalRoot.layer;
            RectTransform panelRect = (RectTransform)panelGo.transform;
            panelRect.SetParent(rootRect, false);
            panelRect.anchorMin = new Vector2(0.08f, 0.10f);
            panelRect.anchorMax = new Vector2(0.92f, 0.90f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelBg = panelGo.GetComponent<Image>();
            panelBg.sprite = solid;
            panelBg.color = new Color(0.09f, 0.10f, 0.15f, 0.96f);

            // Header Title
            _titleText = CreateLabel(panelRect, "Title", font, 36f, FontStyles.Bold, Color.white,
                new Vector2(0f, 1f), new Vector2(0.75f, 1f), new Vector2(28f, -66f), new Vector2(-28f, -16f));
            _titleText.alignment = TextAlignmentOptions.Left;

            _subtitleText = CreateLabel(panelRect, "Subtitle", font, 19f, FontStyles.Normal, new Color(0.75f, 0.80f, 0.90f, 1f),
                new Vector2(0f, 1f), new Vector2(0.85f, 1f), new Vector2(28f, -100f), new Vector2(-28f, -66f));
            _subtitleText.alignment = TextAlignmentOptions.Left;

            // Tabs (Draw / Discard / Exhaust) + Close button
            CreateTabButton(panelRect, "DrawTab", "Draw", font, solid, new Vector2(-380f, -44f), () => OpenPile(CombatPileKind.Draw));
            CreateTabButton(panelRect, "DiscardTab", "Discard", font, solid, new Vector2(-262f, -44f), () => OpenPile(CombatPileKind.Discard));
            CreateTabButton(panelRect, "ExhaustTab", "Exhaust", font, solid, new Vector2(-144f, -44f), () => OpenPile(CombatPileKind.Exhaust));
            CreateTabButton(panelRect, "CloseBtn", "X", font, solid, new Vector2(-48f, -44f), Close, new Vector2(56f, 40f), new Color(0.65f, 0.22f, 0.25f, 1f));

            // ScrollRect
            GameObject scrollGo = new("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.layer = _modalRoot.layer;
            RectTransform scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.SetParent(panelRect, false);
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(24f, 24f);
            scrollRect.offsetMax = new Vector2(-24f, -114f);
            scrollGo.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.65f);

            GameObject viewportGo = new("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.layer = _modalRoot.layer;
            RectTransform viewportRect = (RectTransform)viewportGo.transform;
            viewportRect.SetParent(scrollRect, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().sprite = solid;
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentGo = new("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.layer = _modalRoot.layer;
            RectTransform contentRect = (RectTransform)contentGo.transform;
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            GridLayoutGroup grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(260f, 336f);
            grid.spacing = new Vector2(32f, 26f);
            grid.padding = new RectOffset(36, 36, 26, 26);
            grid.childAlignment = TextAnchor.UpperLeft;

            ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = viewportRect;
            sr.content = contentRect;
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 35f;

            _gridContent = contentRect;

            _emptyText = CreateLabel(scrollRect, "EmptyText", font, 26f, FontStyles.Italic, new Color(0.68f, 0.72f, 0.80f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _emptyText.alignment = TextAlignmentOptions.Center;

            _modalRoot.SetActive(false);
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

        private static void CreateTabButton(
            RectTransform parent, string name, string label, TMP_FontAsset font, Sprite solid,
            Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick,
            Vector2? size = null, Color? bgColor = null)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size ?? new Vector2(108f, 40f);

            Image img = go.GetComponent<Image>();
            img.sprite = solid;
            img.color = bgColor ?? new Color(0.18f, 0.21f, 0.30f, 1f);

            Button btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            TextMeshProUGUI txt = CreateLabel(rect, "Text", font, 18f, FontStyles.Bold, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            txt.alignment = TextAlignmentOptions.Center;
            txt.text = label;
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
