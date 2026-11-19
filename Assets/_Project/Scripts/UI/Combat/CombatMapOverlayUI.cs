using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Map;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI.Map;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    /// <summary>
    /// In-combat Map Peek Modal opened from CombatTopBar's Map scroll button (or 'M' hotkey).
    /// Displays the current run's Left-to-Right procedural map using existing project art assets,
    /// highlights the player's current node with Pin Map.png + pulsing ring, and animates nodes Left->Right.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatMapOverlayUI : MonoBehaviour
    {
        public static bool IsAnyOpen { get; private set; }

        [Header("Scene & Data References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private MapGeneratorConfig _generatorConfig;
        [SerializeField] private MapEncounterPool _encounterPool;

        [Header("Existing Project Map Art Assets")]
        [SerializeField] private Sprite _frameSprite;
        [SerializeField] private Sprite _parchmentSprite;
        [SerializeField] private Sprite _buttonSprite;
        [SerializeField] private Sprite _currentPinSprite;
        [SerializeField] private Sprite _enemyIcon;
        [SerializeField] private Sprite _eliteIcon;
        [SerializeField] private Sprite _bossIcon;
        [SerializeField] private Sprite _eventIcon;
        [SerializeField] private Sprite _shopIcon;
        [SerializeField] private Sprite _restIcon;
        [SerializeField] private Sprite _treasureIcon;

        private GameObject _modalRoot;
        private CanvasGroup _modalGroup;
        private RectTransform _windowRect;
        private TextMeshProUGUI _headerTitle;
        private TextMeshProUGUI _headerSubtitle;
        private ProceduralMapRendererUI _mapRenderer;
        private bool _isOpen;

        public bool IsOpen => _isOpen;
        public ProceduralMapRendererUI MapRenderer => _mapRenderer;

        private void Awake()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_canvas == null) _canvas = FindFirstObjectByType<Canvas>();
        }

        private void OnDisable()
        {
            if (_isOpen)
            {
                _isOpen = false;
                IsAnyOpen = false;
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (_isOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (Keyboard.current.mKey.wasPressedThisFrame)
            {
                Toggle();
            }
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            BuildModalIfNeeded();
            if (_modalRoot == null) return;

            // Ensure a live 6-8 layer procedural map exists even when launching Game.unity directly
            MapGraph graph;
            string currentNodeId;
            if (RunManager.Instance != null)
            {
                graph = RunManager.Instance.EnsureActiveProceduralMap(_generatorConfig, _encounterPool, placeOnStartingCombatNode: true);
                currentNodeId = RunManager.Instance.CurrentMapNodeId;
            }
            else
            {
                graph = ProceduralMapGenerator.Generate(_generatorConfig, _encounterPool);
                MapNode start = graph.GetInitialNodes().Count > 0 ? graph.GetInitialNodes()[0] : null;
                if (start != null) graph.MarkVisited(start.Id);
                currentNodeId = start?.Id;
            }

            _isOpen = true;
            IsAnyOpen = true;

            _modalRoot.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();

            // Update header with live floor / current node progress
            MapNode currentNode = graph.GetCurrentNode(currentNodeId);
            int totalSteps = Mathf.Max(1, graph.LayerCount);
            int currentStep = currentNode != null ? currentNode.Layer + 1 : 1;
            string nodeTypeName = currentNode != null ? currentNode.Type.ToString().ToUpperInvariant() : "START";

            if (_headerTitle != null)
            {
                _headerTitle.text = $"JOURNEY MAP   •   STEP {currentStep} / {totalSteps}";
            }
            if (_headerSubtitle != null)
            {
                _headerSubtitle.text = $"START (LEFT)  →  BOSS (RIGHT)   |   CURRENT NODE: {nodeTypeName}";
            }

            // Unroll scroll animation + trigger Left->Right procedural map pop-in wave
            _modalGroup.DOKill();
            _windowRect.DOKill();
            _modalGroup.alpha = 0f;
            _windowRect.localScale = new Vector3(0.76f, 0.94f, 1f);
            _modalGroup.DOFade(1f, 0.16f).SetUpdate(true);
            _windowRect.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);

            _mapRenderer.RenderMap(graph, currentNodeId, interactive: false, onNodeClicked: null, animateEntrance: true);
            Tune.SFX(SfxID.DrawCard);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            IsAnyOpen = false;

            _mapRenderer?.KillAnimations();
            Tune.SFX(SfxID.ui_cancel);

            if (_modalRoot != null)
            {
                _modalGroup.DOKill();
                _modalRoot.SetActive(false);
            }
        }

        private void BuildModalIfNeeded()
        {
            if (_modalRoot != null || _canvas == null) return;

            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;
            int layer = _canvas.gameObject.layer;

            // Fullscreen click-to-close dim backdrop
            _modalRoot = new GameObject("CombatMapOverlayModal", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
            _modalRoot.layer = layer;
            RectTransform rootRt = (RectTransform)_modalRoot.transform;
            rootRt.SetParent(_canvas.transform, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            _modalGroup = _modalRoot.GetComponent<CanvasGroup>();
            Image dim = _modalRoot.GetComponent<Image>();
            dim.color = new Color(0.04f, 0.03f, 0.02f, 0.80f);

            Button backdropBtn = _modalRoot.GetComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.onClick.AddListener(Close);

            // Unrolled parchment scroll backdrop using existing scroll.png (1780 x 960)
            GameObject winGo = new("MapWindow", typeof(RectTransform), typeof(Image));
            winGo.layer = layer;
            _windowRect = (RectTransform)winGo.transform;
            _windowRect.SetParent(rootRt, false);
            _windowRect.anchorMin = _windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            _windowRect.pivot = new Vector2(0.5f, 0.5f);
            _windowRect.sizeDelta = new Vector2(1780f, 960f);

            Image scrollImg = winGo.GetComponent<Image>();
            Sprite bgSprite = _parchmentSprite != null ? _parchmentSprite : _frameSprite;
            if (bgSprite != null)
            {
                scrollImg.sprite = bgSprite;
                scrollImg.color = Color.white;
            }
            else
            {
                scrollImg.color = new Color(0.84f, 0.68f, 0.42f, 0.98f);
            }
            scrollImg.raycastTarget = true; // Absorb clicks inside scroll

            // Inner flat parchment safe area strictly between the rolled left/right scroll cylinders
            GameObject innerGo = new("ScrollContent", typeof(RectTransform));
            innerGo.layer = layer;
            RectTransform innerRt = (RectTransform)innerGo.transform;
            innerRt.SetParent(_windowRect, false);
            innerRt.anchorMin = new Vector2(0.175f, 0.125f);
            innerRt.anchorMax = new Vector2(0.835f, 0.855f);
            innerRt.offsetMin = Vector2.zero;
            innerRt.offsetMax = Vector2.zero;

            // Inked header directly on the top of the parchment scroll
            _headerTitle = MakeText(innerRt, "HeaderTitle", font, 27f, FontStyles.Bold, new Color(0.24f, 0.14f, 0.06f, 1f), TextAlignmentOptions.Left);
            _headerTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _headerTitle.rectTransform.anchorMax = new Vector2(0.78f, 1f);
            _headerTitle.rectTransform.offsetMin = new Vector2(12f, -44f);
            _headerTitle.rectTransform.offsetMax = new Vector2(0f, -6f);

            _headerSubtitle = MakeText(innerRt, "HeaderSubtitle", font, 13.5f, FontStyles.Bold, new Color(0.38f, 0.25f, 0.12f, 0.92f), TextAlignmentOptions.Left);
            _headerSubtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _headerSubtitle.rectTransform.anchorMax = new Vector2(0.78f, 1f);
            _headerSubtitle.rectTransform.offsetMin = new Vector2(12f, -66f);
            _headerSubtitle.rectTransform.offsetMax = new Vector2(0f, -42f);

            // Close button styled with existing Tombol.png tucked inside top-right of parchment
            GameObject closeGo = new("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.layer = layer;
            RectTransform closeRt = (RectTransform)closeGo.transform;
            closeRt.SetParent(innerRt, false);
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-10f, -8f);
            closeRt.sizeDelta = new Vector2(138f, 44f);

            Image closeImg = closeGo.GetComponent<Image>();
            if (_buttonSprite != null)
            {
                closeImg.sprite = _buttonSprite;
                closeImg.color = Color.white;
            }
            else
            {
                closeImg.color = new Color(0.68f, 0.54f, 0.34f, 1f);
            }

            Button closeBtn = closeGo.GetComponent<Button>();
            closeBtn.onClick.AddListener(Close);

            TextMeshProUGUI closeLbl = MakeText(closeRt, "Label", font, 18f, FontStyles.Bold, new Color(0.22f, 0.14f, 0.06f, 1f), TextAlignmentOptions.Center);
            closeLbl.rectTransform.anchorMin = Vector2.zero;
            closeLbl.rectTransform.anchorMax = Vector2.one;
            closeLbl.rectTransform.offsetMin = Vector2.zero;
            closeLbl.rectTransform.offsetMax = Vector2.zero;
            closeLbl.text = "CLOSE";

            // Transparent ProceduralMapBoard drawing nodes & dotted trails straight onto scroll.png!
            GameObject boardGo = new("ProceduralMapBoard", typeof(RectTransform), typeof(ProceduralMapRendererUI));
            boardGo.layer = layer;
            RectTransform boardRt = (RectTransform)boardGo.transform;
            boardRt.SetParent(innerRt, false);
            boardRt.anchorMin = Vector2.zero;
            boardRt.anchorMax = Vector2.one;
            boardRt.offsetMin = new Vector2(0f, 0f);
            boardRt.offsetMax = new Vector2(0f, -68f);

            _mapRenderer = boardGo.GetComponent<ProceduralMapRendererUI>();
            _mapRenderer.SetShowLegendBar(true);
            _mapRenderer.SetRenderOwnParchmentBackground(false);
            _mapRenderer.ConfigureAssets(
                null, _currentPinSprite,
                _enemyIcon, _eliteIcon, _bossIcon,
                _eventIcon, _shopIcon, _restIcon, _treasureIcon,
                font);

            _modalRoot.SetActive(false);
        }

        private static TextMeshProUGUI MakeText(
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
    }
}

