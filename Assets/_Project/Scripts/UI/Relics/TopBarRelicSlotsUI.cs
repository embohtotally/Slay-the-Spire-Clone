using System;
using System.Collections.Generic;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Relics
{
    /// <summary>
    /// Renders the 5 Relic template slots in the top-middle of the Top Bar (Figma layout).
    /// Supports displaying equipped relics, hover tooltips, drag-and-drop slot reordering,
    /// and clicking to open the RelicSwapModalUI.
    /// </summary>
    [DisallowMultipleComponent]
    public class TopBarRelicSlotsUI : MonoBehaviour
    {
        [Serializable]
        public class RelicSlotVisual
        {
            public RectTransform Root;
            public Image Background;
            public Image Border;
            public Image IconImage;
            public TMP_Text FallbackLabel;
        }

        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RelicSwapModalUI _swapModal;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private List<RelicSlotVisual> _slots = new();

        [Header("Colors")]
        [SerializeField] private Color _emptyBgColor = new(0.87f, 0.87f, 0.87f, 0.92f);
        [SerializeField] private Color _emptyBorderColor = new(0.62f, 0.56f, 0.42f, 0.85f);
        [SerializeField] private Color _filledBgColor = new(0.18f, 0.16f, 0.14f, 0.96f);
        [SerializeField] private Color _filledBorderColor = new(0.92f, 0.76f, 0.36f, 1f);
        [SerializeField] private Color _dropHighlightColor = new(0.32f, 0.86f, 1f, 1f);

        private RunRelicManager _subscribedManager;
        private int _draggingSlotIndex = -1;
        private RectTransform _dragGhost;
        private Image _dragGhostIcon;
        private TextMeshProUGUI _dragGhostText;
        private RectTransform _tooltipRoot;
        private TextMeshProUGUI _tooltipTitle;
        private TextMeshProUGUI _tooltipBody;

        private void Awake()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_swapModal == null) _swapModal = FindFirstObjectByType<RelicSwapModalUI>();
            AttachSlotHandlers();
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshSlots();
        }

        private void OnDisable()
        {
            Unsubscribe();
            HideTooltip();
            EndDragVisual();
        }

        private void Update()
        {
            if (_subscribedManager != RunRelicManager.Instance)
            {
                Unsubscribe();
                Subscribe();
                RefreshSlots();
            }
        }

        public void RefreshSlots()
        {
            RunRelicManager manager = RunRelicManager.Instance;
            IReadOnlyList<RelicData> equipped = manager != null ? manager.EquippedSlots : null;

            for (int i = 0; i < _slots.Count; i++)
            {
                RelicSlotVisual slot = _slots[i];
                if (slot == null) continue;

                RelicData relic = equipped != null && i < equipped.Count ? equipped[i] : null;
                BindSlotVisual(slot, relic, i == _draggingSlotIndex);
            }
        }

        private void BindSlotVisual(RelicSlotVisual slot, RelicData relic, bool isBeingDragged)
        {
            bool hasRelic = relic != null;

            if (slot.Background != null)
            {
                Color bg = hasRelic ? _filledBgColor : _emptyBgColor;
                if (isBeingDragged) bg.a = 0.45f;
                slot.Background.color = bg;
            }

            if (slot.Border != null)
            {
                slot.Border.color = hasRelic ? GetRarityBorderColor(relic.Rarity, _filledBorderColor) : _emptyBorderColor;
            }

            if (slot.IconImage != null)
            {
                bool showSprite = hasRelic && relic.Image != null;
                slot.IconImage.sprite = showSprite ? relic.Image : null;
                slot.IconImage.enabled = showSprite;
                slot.IconImage.color = isBeingDragged ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
            }

            if (slot.FallbackLabel != null)
            {
                bool showFallback = hasRelic && relic.Image == null;
                slot.FallbackLabel.gameObject.SetActive(showFallback);
                if (showFallback)
                {
                    slot.FallbackLabel.text = BuildMonogram(relic.Title);
                }
            }
        }

        private void AttachSlotHandlers()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i]?.Root == null) continue;
                TopBarRelicSlotHandler handler = _slots[i].Root.GetComponent<TopBarRelicSlotHandler>();
                if (handler == null) handler = _slots[i].Root.gameObject.AddComponent<TopBarRelicSlotHandler>();
                handler.Init(this, i);
            }
        }

        internal void HandleSlotHoverEnter(int slotIndex)
        {
            if (_draggingSlotIndex >= 0)
            {
                if (slotIndex >= 0 && slotIndex < _slots.Count && _slots[slotIndex].Border != null)
                    _slots[slotIndex].Border.color = _dropHighlightColor;
                return;
            }

            RelicData relic = RunRelicManager.Instance != null ? RunRelicManager.Instance.GetRelicAt(slotIndex) : null;
            if (relic == null) return;
            ShowTooltip(relic, _slots[slotIndex].Root);
        }

        internal void HandleSlotHoverExit(int slotIndex)
        {
            HideTooltip();
            if (_draggingSlotIndex >= 0) RefreshSlots();
        }

        internal void HandleSlotClick(int slotIndex)
        {
            if (_draggingSlotIndex >= 0) return;
            if (_swapModal == null) _swapModal = FindFirstObjectByType<RelicSwapModalUI>();
            if (_swapModal != null)
            {
                _swapModal.OpenManageMode();
            }
        }

        internal void HandleBeginDrag(int slotIndex, PointerEventData eventData)
        {
            RelicData relic = RunRelicManager.Instance != null ? RunRelicManager.Instance.GetRelicAt(slotIndex) : null;
            if (relic == null) return;

            HideTooltip();
            _draggingSlotIndex = slotIndex;
            Tune.SFX(SfxID.card_pickup);
            EnsureDragGhost();
            if (_dragGhost != null)
            {
                _dragGhost.gameObject.SetActive(true);
                _dragGhost.SetAsLastSibling();
                bool hasImg = relic.Image != null;
                _dragGhostIcon.sprite = hasImg ? relic.Image : null;
                _dragGhostIcon.enabled = hasImg;
                _dragGhostText.gameObject.SetActive(!hasImg);
                if (!hasImg) _dragGhostText.text = BuildMonogram(relic.Title);
                MoveDragGhost(eventData.position);
            }
            RefreshSlots();
        }

        internal void HandleDrag(PointerEventData eventData)
        {
            if (_draggingSlotIndex < 0) return;
            MoveDragGhost(eventData.position);
        }

        internal void HandleDrop(int targetSlotIndex)
        {
            if (_draggingSlotIndex < 0 || RunRelicManager.Instance == null) return;
            if (_draggingSlotIndex != targetSlotIndex)
            {
                RunRelicManager.Instance.SwapSlots(_draggingSlotIndex, targetSlotIndex);
                Tune.SFX(SfxID.relic_claim);
            }
            EndDragVisual();
            RefreshSlots();
        }

        internal void HandleEndDrag()
        {
            if (_draggingSlotIndex < 0) return;
            EndDragVisual();
            RefreshSlots();
        }

        private void EndDragVisual()
        {
            _draggingSlotIndex = -1;
            if (_dragGhost != null) _dragGhost.gameObject.SetActive(false);
        }

        private void MoveDragGhost(Vector2 screenPos)
        {
            if (_dragGhost == null || _canvas == null || _canvas.transform is not RectTransform canvasRect) return;
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out Vector2 local))
            {
                _dragGhost.anchoredPosition = local;
            }
        }

        private void EnsureDragGhost()
        {
            if (_dragGhost != null || _canvas == null) return;

            GameObject go = new("TopBarRelicDragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.layer = _canvas.gameObject.layer;
            _dragGhost = (RectTransform)go.transform;
            _dragGhost.SetParent(_canvas.transform, false);
            _dragGhost.anchorMin = _dragGhost.anchorMax = new Vector2(0.5f, 0.5f);
            _dragGhost.pivot = new Vector2(0.5f, 0.5f);
            _dragGhost.sizeDelta = new Vector2(54f, 54f);

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.14f, 0.13f, 0.11f, 0.94f);
            bg.raycastTarget = false;

            GameObject iconGo = new("Icon", typeof(RectTransform), typeof(Image));
            iconGo.layer = go.layer;
            RectTransform iconRt = (RectTransform)iconGo.transform;
            iconRt.SetParent(_dragGhost, false);
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(5f, 5f);
            iconRt.offsetMax = new Vector2(-5f, -5f);
            _dragGhostIcon = iconGo.GetComponent<Image>();
            _dragGhostIcon.preserveAspect = true;
            _dragGhostIcon.raycastTarget = false;

            GameObject txtGo = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.layer = go.layer;
            RectTransform txtRt = (RectTransform)txtGo.transform;
            txtRt.SetParent(_dragGhost, false);
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            _dragGhostText = txtGo.GetComponent<TextMeshProUGUI>();
            _dragGhostText.fontSize = 18f;
            _dragGhostText.fontStyle = FontStyles.Bold;
            _dragGhostText.alignment = TextAlignmentOptions.Center;
            _dragGhostText.color = new Color(1f, 0.88f, 0.52f, 1f);
            _dragGhostText.raycastTarget = false;
            if (_font != null) _dragGhostText.font = _font;

            go.SetActive(false);
        }

        private void ShowTooltip(RelicData relic, RectTransform anchor)
        {
            if (_canvas == null || _canvas.transform is not RectTransform canvasRect) return;
            if (_tooltipRoot == null)
            {
                GameObject go = new("TopBarRelicTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                go.layer = _canvas.gameObject.layer;
                _tooltipRoot = (RectTransform)go.transform;
                _tooltipRoot.SetParent(_canvas.transform, false);
                _tooltipRoot.anchorMin = _tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
                _tooltipRoot.pivot = new Vector2(0.5f, 1f);
                _tooltipRoot.sizeDelta = new Vector2(300f, 90f);

                CanvasGroup cg = go.GetComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;

                go.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.95f);

                VerticalLayoutGroup vlg = go.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(14, 14, 10, 10);
                vlg.spacing = 4f;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                GameObject tGo = new("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                tGo.layer = go.layer;
                tGo.transform.SetParent(_tooltipRoot, false);
                _tooltipTitle = tGo.GetComponent<TextMeshProUGUI>();
                _tooltipTitle.fontSize = 19f;
                _tooltipTitle.fontStyle = FontStyles.Bold;
                _tooltipTitle.color = new Color(1f, 0.85f, 0.42f, 1f);
                _tooltipTitle.raycastTarget = false;
                if (_font != null) _tooltipTitle.font = _font;

                GameObject bGo = new("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
                bGo.layer = go.layer;
                bGo.transform.SetParent(_tooltipRoot, false);
                _tooltipBody = bGo.GetComponent<TextMeshProUGUI>();
                _tooltipBody.fontSize = 15f;
                _tooltipBody.color = new Color(0.90f, 0.92f, 0.96f, 1f);
                _tooltipBody.textWrappingMode = TextWrappingModes.Normal;
                _tooltipBody.raycastTarget = false;
                if (_font != null) _tooltipBody.font = _font;
            }

            _tooltipTitle.text = $"{relic.Title}  <size=75%><color=#B0B8C8>({relic.Rarity})</color></size>";
            _tooltipBody.text = string.IsNullOrWhiteSpace(relic.Description)
                ? "Click to manage relics or drag to reorder."
                : $"{relic.Description}\n<size=80%><color=#8FA4C0>Drag to reorder • Click to manage</color></size>";

            _tooltipRoot.gameObject.SetActive(true);
            _tooltipRoot.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRoot);

            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, anchor.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out Vector2 local))
            {
                _tooltipRoot.anchoredPosition = new Vector2(local.x, local.y - 38f);
            }
        }

        private void HideTooltip()
        {
            if (_tooltipRoot != null && _tooltipRoot.gameObject.activeSelf)
                _tooltipRoot.gameObject.SetActive(false);
        }

        private void Subscribe()
        {
            RunRelicManager.AnyRelicsChanged -= HandleRelicsChanged;
            RunRelicManager.AnyRelicsChanged += HandleRelicsChanged;
            _subscribedManager = RunRelicManager.Instance;
        }

        private void Unsubscribe()
        {
            RunRelicManager.AnyRelicsChanged -= HandleRelicsChanged;
            _subscribedManager = null;
        }

        private void HandleRelicsChanged(IReadOnlyList<RelicData> _) => RefreshSlots();

        public static string BuildMonogram(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "R";
            string[] parts = title.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
            return parts[0].Length >= 2
                ? parts[0].Substring(0, 2).ToUpperInvariant()
                : parts[0].ToUpperInvariant();
        }

        public static Color GetRarityBorderColor(RelicRarity rarity, Color fallback)
        {
            return rarity switch
            {
                RelicRarity.Uncommon => new Color(0.32f, 0.80f, 1f, 1f),
                RelicRarity.Rare => new Color(0.98f, 0.78f, 0.25f, 1f),
                RelicRarity.Boss => new Color(1f, 0.36f, 0.36f, 1f),
                RelicRarity.Special => new Color(0.78f, 0.45f, 1f, 1f),
                _ => fallback
            };
        }
    }

    internal sealed class TopBarRelicSlotHandler : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private TopBarRelicSlotsUI _owner;
        private int _index;

        public void Init(TopBarRelicSlotsUI owner, int index)
        {
            _owner = owner;
            _index = index;
        }

        public void OnPointerEnter(PointerEventData eventData) => _owner?.HandleSlotHoverEnter(_index);
        public void OnPointerExit(PointerEventData eventData) => _owner?.HandleSlotHoverExit(_index);
        public void OnPointerClick(PointerEventData eventData) => _owner?.HandleSlotClick(_index);
        public void OnBeginDrag(PointerEventData eventData) => _owner?.HandleBeginDrag(_index, eventData);
        public void OnDrag(PointerEventData eventData) => _owner?.HandleDrag(eventData);
        public void OnEndDrag(PointerEventData eventData) => _owner?.HandleEndDrag();
        public void OnDrop(PointerEventData eventData) => _owner?.HandleDrop(_index);
    }
}

