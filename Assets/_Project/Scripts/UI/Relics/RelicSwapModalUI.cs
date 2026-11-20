using System.Collections.Generic;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Relics
{
    public enum RelicDragZone
    {
        None,
        EquippedSlot,
        IncomingTray,
        UnequipTray
    }

    /// <summary>
    /// Drag-and-drop Relic Equip / Unequip / Swap modal triggered automatically when all 5
    /// relic slots are full and the player receives a new relic (or opened manually by clicking
    /// a Top Bar relic slot).
    /// </summary>
    [DisallowMultipleComponent]
    public class RelicSwapModalUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TMP_FontAsset _font;

        private sealed class ModalSlot
        {
            public RelicDragZone Zone;
            public int SlotIndex;
            public RectTransform Root;
            public Image Background;
            public Image Border;
            public Image Icon;
            public TextMeshProUGUI Monogram;
            public TextMeshProUGUI Title;
        }

        private static Sprite _solidSprite;

        private GameObject _modalRoot;
        private TextMeshProUGUI _headerText;
        private TextMeshProUGUI _subHeaderText;
        private TextMeshProUGUI _detailTitleText;
        private TextMeshProUGUI _detailBodyText;
        private Button _confirmButton;
        private Button _cancelButton;
        private TextMeshProUGUI _cancelButtonLabel;

        private readonly List<ModalSlot> _equippedSlots = new();
        private ModalSlot _incomingSlot;
        private ModalSlot _unequipSlot;

        // Staging state inside the modal so the player can drag freely before clicking Confirm
        private readonly List<RelicData> _stagedEquipped = new();
        private RelicData _stagedIncoming;
        private RelicData _stagedUnequipped;
        private RelicData _originalPending;
        private bool _isPendingSwapMode;
        private bool _isOpen;

        private RelicDragZone _dragSourceZone = RelicDragZone.None;
        private int _dragSourceIndex = -1;
        private RelicData _draggingRelic;
        private RectTransform _dragGhost;
        private Image _dragGhostIcon;
        private TextMeshProUGUI _dragGhostText;

        private RunRelicManager _subscribedManager;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            EndDragVisual();
        }

        private void Update()
        {
            if (_subscribedManager != RunRelicManager.Instance)
            {
                Unsubscribe();
                Subscribe();
            }

            if (_isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelAndClose();
            }
        }

        public void OpenForPendingRelic(RelicData incomingRelic)
        {
            if (incomingRelic == null) return;
            OpenInternal(incomingRelic, isPendingMode: true);
        }

        public void OpenManageMode()
        {
            RelicData pending = RunRelicManager.Instance != null ? RunRelicManager.Instance.PendingRelic : null;
            OpenInternal(pending, isPendingMode: pending != null);
        }

        private void OpenInternal(RelicData incomingRelic, bool isPendingMode)
        {
            BuildModalIfNeeded();
            if (_modalRoot == null) return;

            RunRelicManager manager = RunRelicManager.EnsureInstance();
            _stagedEquipped.Clear();
            int maxSlots = manager.MaxEquippedRelics;
            for (int i = 0; i < maxSlots; i++)
            {
                _stagedEquipped.Add(manager.GetRelicAt(i));
            }

            _originalPending = incomingRelic;
            _stagedIncoming = incomingRelic;
            _stagedUnequipped = null;
            _isPendingSwapMode = isPendingMode;
            _isOpen = true;

            _modalRoot.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();
            RefreshModalVisuals();
            ShowDetails(incomingRelic ?? FirstNonNull(_stagedEquipped));
        }

        public void ConfirmAndClose()
        {
            RunRelicManager manager = RunRelicManager.EnsureInstance();
            manager.ApplySlotLoadout(_stagedEquipped);
            manager.ClearPendingRelic();
            Tune.SFX(SfxID.relic_claim);
            CloseInternal();
        }

        public void CancelAndClose()
        {
            if (_isPendingSwapMode && RunRelicManager.Instance != null)
            {
                RunRelicManager.Instance.ClearPendingRelic();
            }
            Tune.SFX(SfxID.ui_cancel);
            CloseInternal();
        }

        private void CloseInternal()
        {
            EndDragVisual();
            _isOpen = false;
            if (_modalRoot != null) _modalRoot.SetActive(false);
        }

        private void Subscribe()
        {
            RunRelicManager.AnyRelicSlotsFullRequested -= OpenForPendingRelic;
            RunRelicManager.AnyRelicSlotsFullRequested += OpenForPendingRelic;
            _subscribedManager = RunRelicManager.Instance;
        }

        private void Unsubscribe()
        {
            RunRelicManager.AnyRelicSlotsFullRequested -= OpenForPendingRelic;
            _subscribedManager = null;
        }

        // ------------------------------------------------------------------
        // Drag & Drop State Machine
        // ------------------------------------------------------------------
        internal void OnSlotHoverEnter(RelicDragZone zone, int index)
        {
            RelicData relic = GetStagedRelic(zone, index);
            if (relic != null) ShowDetails(relic);

            if (_draggingRelic != null)
            {
                ModalSlot slot = GetModalSlot(zone, index);
                if (slot?.Border != null) slot.Border.color = new Color(0.32f, 0.88f, 1f, 1f);
            }
        }

        internal void OnSlotHoverExit(RelicDragZone zone, int index)
        {
            if (_draggingRelic != null) RefreshModalVisuals();
        }

        internal void OnSlotBeginDrag(RelicDragZone zone, int index, PointerEventData eventData)
        {
            RelicData relic = GetStagedRelic(zone, index);
            if (relic == null) return;

            _dragSourceZone = zone;
            _dragSourceIndex = index;
            _draggingRelic = relic;
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
                if (!hasImg) _dragGhostText.text = TopBarRelicSlotsUI.BuildMonogram(relic.Title);
                MoveDragGhost(eventData.position);
            }
            RefreshModalVisuals();
        }

        internal void OnSlotDrag(PointerEventData eventData)
        {
            if (_draggingRelic == null) return;
            MoveDragGhost(eventData.position);
        }

        internal void OnSlotDrop(RelicDragZone targetZone, int targetIndex)
        {
            if (_draggingRelic == null) return;
            if (_dragSourceZone == targetZone && _dragSourceIndex == targetIndex)
            {
                EndDragVisual();
                RefreshModalVisuals();
                return;
            }

            RelicData sourceRelic = GetStagedRelic(_dragSourceZone, _dragSourceIndex);
            RelicData targetRelic = GetStagedRelic(targetZone, targetIndex);

            // Execute swap/move cleanly between any of: EquippedSlot (0..4), IncomingTray, UnequipTray
            if (targetZone == RelicDragZone.EquippedSlot && targetIndex >= 0 && targetIndex < _stagedEquipped.Count)
            {
                if (_dragSourceZone == RelicDragZone.EquippedSlot)
                {
                    _stagedEquipped[targetIndex] = sourceRelic;
                    _stagedEquipped[_dragSourceIndex] = targetRelic;
                }
                else if (_dragSourceZone == RelicDragZone.IncomingTray)
                {
                    _stagedEquipped[targetIndex] = sourceRelic;
                    _stagedIncoming = null;
                    if (targetRelic != null) _stagedUnequipped = targetRelic;
                }
                else if (_dragSourceZone == RelicDragZone.UnequipTray)
                {
                    _stagedEquipped[targetIndex] = sourceRelic;
                    _stagedUnequipped = targetRelic;
                }
                Tune.SFX(SfxID.relic_claim);
            }
            else if (targetZone == RelicDragZone.UnequipTray)
            {
                if (_dragSourceZone == RelicDragZone.EquippedSlot)
                {
                    _stagedEquipped[_dragSourceIndex] = targetRelic; // if something was in unequip tray, swap back cleanly!
                    _stagedUnequipped = sourceRelic;
                }
                else if (_dragSourceZone == RelicDragZone.IncomingTray)
                {
                    _stagedIncoming = targetRelic;
                    _stagedUnequipped = sourceRelic;
                }
                Tune.SFX(SfxID.card_discard);
            }
            else if (targetZone == RelicDragZone.IncomingTray)
            {
                if (_dragSourceZone == RelicDragZone.EquippedSlot)
                {
                    _stagedEquipped[_dragSourceIndex] = targetRelic;
                    _stagedIncoming = sourceRelic;
                }
                else if (_dragSourceZone == RelicDragZone.UnequipTray)
                {
                    _stagedUnequipped = targetRelic;
                    _stagedIncoming = sourceRelic;
                }
                Tune.SFX(SfxID.card_pickup);
            }

            ShowDetails(sourceRelic);
            EndDragVisual();
            RefreshModalVisuals();
        }

        internal void OnSlotEndDrag()
        {
            EndDragVisual();
            RefreshModalVisuals();
        }

        private RelicData GetStagedRelic(RelicDragZone zone, int index)
        {
            return zone switch
            {
                RelicDragZone.EquippedSlot => index >= 0 && index < _stagedEquipped.Count ? _stagedEquipped[index] : null,
                RelicDragZone.IncomingTray => _stagedIncoming,
                RelicDragZone.UnequipTray => _stagedUnequipped,
                _ => null
            };
        }

        private ModalSlot GetModalSlot(RelicDragZone zone, int index)
        {
            return zone switch
            {
                RelicDragZone.EquippedSlot => index >= 0 && index < _equippedSlots.Count ? _equippedSlots[index] : null,
                RelicDragZone.IncomingTray => _incomingSlot,
                RelicDragZone.UnequipTray => _unequipSlot,
                _ => null
            };
        }

        private void RefreshModalVisuals()
        {
            int filledCount = 0;
            for (int i = 0; i < _stagedEquipped.Count; i++)
                if (_stagedEquipped[i] != null) filledCount++;

            if (_headerText != null)
            {
                _headerText.text = _isPendingSwapMode
                    ? $"RELIC SLOTS FULL  ({filledCount} / {_stagedEquipped.Count})"
                    : $"EQUIPPED RELICS  ({filledCount} / {_stagedEquipped.Count})";
            }

            if (_subHeaderText != null)
            {
                _subHeaderText.text = _isPendingSwapMode
                    ? "Drag an equipped relic into Unequip to free a slot, or drag the new relic onto any slot to swap."
                    : "Drag relics to reorder slots, or drag a relic into Unequip to remove it.";
            }

            if (_cancelButtonLabel != null)
            {
                _cancelButtonLabel.text = _isPendingSwapMode ? "Skip New Relic" : "Cancel";
            }

            for (int i = 0; i < _equippedSlots.Count; i++)
            {
                RelicData relic = i < _stagedEquipped.Count ? _stagedEquipped[i] : null;
                bool dragging = _dragSourceZone == RelicDragZone.EquippedSlot && _dragSourceIndex == i;
                BindModalSlot(_equippedSlots[i], relic, $"Slot {i + 1}", dragging,
                    emptyBg: new Color(0.18f, 0.19f, 0.24f, 0.92f),
                    emptyBorder: new Color(0.55f, 0.50f, 0.38f, 0.85f));
            }

            BindModalSlot(_incomingSlot, _stagedIncoming, "New Relic (Empty)",
                _dragSourceZone == RelicDragZone.IncomingTray,
                emptyBg: new Color(0.12f, 0.16f, 0.22f, 0.88f),
                emptyBorder: new Color(0.32f, 0.72f, 0.96f, 0.9f));

            BindModalSlot(_unequipSlot, _stagedUnequipped, "Drop Here to Unequip",
                _dragSourceZone == RelicDragZone.UnequipTray,
                emptyBg: new Color(0.22f, 0.12f, 0.14f, 0.88f),
                emptyBorder: new Color(0.88f, 0.34f, 0.34f, 0.9f));
        }

        private static void BindModalSlot(ModalSlot slot, RelicData relic, string emptyLabel, bool isDragging, Color emptyBg, Color emptyBorder)
        {
            if (slot == null) return;
            bool hasRelic = relic != null;

            if (slot.Background != null)
            {
                Color c = hasRelic ? new Color(0.14f, 0.13f, 0.11f, 0.96f) : emptyBg;
                if (isDragging) c.a = 0.4f;
                slot.Background.color = c;
            }

            if (slot.Border != null)
            {
                slot.Border.color = hasRelic
                    ? TopBarRelicSlotsUI.GetRarityBorderColor(relic.Rarity, new Color(0.92f, 0.76f, 0.36f, 1f))
                    : emptyBorder;
            }

            if (slot.Icon != null)
            {
                bool showImg = hasRelic && relic.Image != null;
                slot.Icon.sprite = showImg ? relic.Image : null;
                slot.Icon.enabled = showImg;
                slot.Icon.color = isDragging ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
            }

            if (slot.Monogram != null)
            {
                bool showMono = hasRelic && relic.Image == null;
                slot.Monogram.gameObject.SetActive(showMono);
                if (showMono) slot.Monogram.text = TopBarRelicSlotsUI.BuildMonogram(relic.Title);
            }

            if (slot.Title != null)
            {
                slot.Title.text = hasRelic ? relic.Title : emptyLabel;
                slot.Title.color = hasRelic ? Color.white : new Color(0.70f, 0.74f, 0.80f, 0.9f);
            }
        }

        private void ShowDetails(RelicData relic)
        {
            if (_detailTitleText == null || _detailBodyText == null) return;
            if (relic == null)
            {
                _detailTitleText.text = "Hover or drag a relic to inspect";
                _detailBodyText.text = "You can equip up to 5 relics at once.";
                return;
            }

            _detailTitleText.text = $"{relic.Title}  <size=78%><color=#B0B8C8>({relic.Rarity})</color></size>";
            _detailBodyText.text = string.IsNullOrWhiteSpace(relic.Description) ? "No description." : relic.Description;
        }

        private void EndDragVisual()
        {
            _dragSourceZone = RelicDragZone.None;
            _dragSourceIndex = -1;
            _draggingRelic = null;
            if (_dragGhost != null) _dragGhost.gameObject.SetActive(false);
        }

        private void MoveDragGhost(Vector2 screenPos)
        {
            if (_dragGhost == null || _canvas == null || _canvas.transform is not RectTransform canvasRect) return;
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out Vector2 local))
                _dragGhost.anchoredPosition = local;
        }

        private void EnsureDragGhost()
        {
            if (_dragGhost != null || _canvas == null) return;
            Sprite solid = GetSolidSprite();
            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;

            GameObject go = new("RelicSwapDragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.layer = _canvas.gameObject.layer;
            _dragGhost = (RectTransform)go.transform;
            _dragGhost.SetParent(_canvas.transform, false);
            _dragGhost.anchorMin = _dragGhost.anchorMax = new Vector2(0.5f, 0.5f);
            _dragGhost.pivot = new Vector2(0.5f, 0.5f);
            _dragGhost.sizeDelta = new Vector2(84f, 84f);

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            Image bg = go.GetComponent<Image>();
            bg.sprite = solid;
            bg.color = new Color(0.14f, 0.13f, 0.11f, 0.94f);
            bg.raycastTarget = false;

            GameObject iconGo = new("Icon", typeof(RectTransform), typeof(Image));
            iconGo.layer = go.layer;
            RectTransform iconRt = (RectTransform)iconGo.transform;
            iconRt.SetParent(_dragGhost, false);
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(8f, 8f);
            iconRt.offsetMax = new Vector2(-8f, -8f);
            _dragGhostIcon = iconGo.GetComponent<Image>();
            _dragGhostIcon.preserveAspect = true;
            _dragGhostIcon.raycastTarget = false;

            GameObject txtGo = new("Mono", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.layer = go.layer;
            RectTransform txtRt = (RectTransform)txtGo.transform;
            txtRt.SetParent(_dragGhost, false);
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            _dragGhostText = txtGo.GetComponent<TextMeshProUGUI>();
            _dragGhostText.fontSize = 26f;
            _dragGhostText.fontStyle = FontStyles.Bold;
            _dragGhostText.alignment = TextAlignmentOptions.Center;
            _dragGhostText.color = new Color(1f, 0.88f, 0.52f, 1f);
            _dragGhostText.raycastTarget = false;
            if (font != null) _dragGhostText.font = font;

            go.SetActive(false);
        }

        private void BuildModalIfNeeded()
        {
            if (_modalRoot != null) return;
            if (_canvas == null) _canvas = FindFirstObjectByType<Canvas>();
            if (_canvas == null) return;

            Sprite solid = GetSolidSprite();
            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;
            int layer = _canvas.gameObject.layer;

            _modalRoot = new GameObject("RelicSwapModal", typeof(RectTransform), typeof(Image));
            _modalRoot.layer = layer;
            RectTransform rootRect = (RectTransform)_modalRoot.transform;
            rootRect.SetParent(_canvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Image dim = _modalRoot.GetComponent<Image>();
            dim.sprite = solid;
            dim.color = new Color(0.03f, 0.04f, 0.07f, 0.84f);

            // Center Window (980 x 620)
            GameObject winGo = new("Window", typeof(RectTransform), typeof(Image));
            winGo.layer = layer;
            RectTransform winRect = (RectTransform)winGo.transform;
            winRect.SetParent(rootRect, false);
            winRect.anchorMin = winRect.anchorMax = new Vector2(0.5f, 0.5f);
            winRect.pivot = new Vector2(0.5f, 0.5f);
            winRect.sizeDelta = new Vector2(980f, 620f);
            Image winBg = winGo.GetComponent<Image>();
            winBg.sprite = solid;
            winBg.color = new Color(0.10f, 0.11f, 0.15f, 0.97f);

            // Top Gold Bar Accent
            GameObject accent = new("GoldAccent", typeof(RectTransform), typeof(Image));
            accent.layer = layer;
            RectTransform accRt = (RectTransform)accent.transform;
            accRt.SetParent(winRect, false);
            accRt.anchorMin = new Vector2(0f, 1f);
            accRt.anchorMax = new Vector2(1f, 1f);
            accRt.pivot = new Vector2(0.5f, 1f);
            accRt.sizeDelta = new Vector2(0f, 5f);
            Image accImg = accent.GetComponent<Image>();
            accImg.sprite = solid;
            accImg.color = new Color(0.90f, 0.72f, 0.30f, 1f);
            accImg.raycastTarget = false;

            _headerText = MakeLabel(winRect, "Header", font, 32f, FontStyles.Bold, new Color(1f, 0.90f, 0.56f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -60f), new Vector2(-30f, -16f));
            _subHeaderText = MakeLabel(winRect, "SubHeader", font, 18f, FontStyles.Normal, new Color(0.80f, 0.84f, 0.92f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -96f), new Vector2(-30f, -60f));

            // 5 Equipped Slots Row
            GameObject rowGo = new("EquippedSlotsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.layer = layer;
            RectTransform rowRt = (RectTransform)rowGo.transform;
            rowRt.SetParent(winRect, false);
            rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -120f);
            rowRt.sizeDelta = new Vector2(880f, 148f);
            HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 22f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            _equippedSlots.Clear();
            int maxSlots = RunRelicManager.Instance != null ? RunRelicManager.Instance.MaxEquippedRelics : RunRelicManager.DefaultMaxEquippedRelics;
            for (int i = 0; i < maxSlots; i++)
            {
                _equippedSlots.Add(BuildModalSlot(rowRt, $"EquippedSlot_{i + 1}", RelicDragZone.EquippedSlot, i, new Vector2(154f, 144f), solid, font));
            }

            // Bottom Trays: Incoming Relic (Left) + Unequip Drop Zone (Right)
            TextMeshProUGUI incomingHeader = MakeLabel(winRect, "IncomingHeader", font, 14f, FontStyles.Bold, new Color(1f, 0.86f, 0.46f, 0.95f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            incomingHeader.rectTransform.pivot = new Vector2(0.5f, 1f);
            incomingHeader.rectTransform.anchoredPosition = new Vector2(-180f, -280f);
            incomingHeader.rectTransform.sizeDelta = new Vector2(260f, 24f);
            incomingHeader.alignment = TextAlignmentOptions.Center;
            incomingHeader.text = "NEW RELIC";

            TextMeshProUGUI unequipHeader = MakeLabel(winRect, "UnequipHeader", font, 14f, FontStyles.Bold, new Color(1f, 0.56f, 0.56f, 0.95f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            unequipHeader.rectTransform.pivot = new Vector2(0.5f, 1f);
            unequipHeader.rectTransform.anchoredPosition = new Vector2(180f, -280f);
            unequipHeader.rectTransform.sizeDelta = new Vector2(260f, 24f);
            unequipHeader.alignment = TextAlignmentOptions.Center;
            unequipHeader.text = "UNEQUIP ZONE";

            _incomingSlot = BuildModalSlot(winRect, "IncomingRelicSlot", RelicDragZone.IncomingTray, -1, new Vector2(260f, 134f), solid, font);
            _incomingSlot.Root.anchorMin = _incomingSlot.Root.anchorMax = new Vector2(0.5f, 1f);
            _incomingSlot.Root.pivot = new Vector2(0.5f, 1f);
            _incomingSlot.Root.anchoredPosition = new Vector2(-180f, -310f);

            _unequipSlot = BuildModalSlot(winRect, "UnequipDropZone", RelicDragZone.UnequipTray, -1, new Vector2(260f, 134f), solid, font);
            _unequipSlot.Root.anchorMin = _unequipSlot.Root.anchorMax = new Vector2(0.5f, 1f);
            _unequipSlot.Root.pivot = new Vector2(0.5f, 1f);
            _unequipSlot.Root.anchoredPosition = new Vector2(180f, -310f);

            // Live Relic Detail Box
            GameObject detailGo = new("DetailBox", typeof(RectTransform), typeof(Image));
            detailGo.layer = layer;
            RectTransform detRt = (RectTransform)detailGo.transform;
            detRt.SetParent(winRect, false);
            detRt.anchorMin = new Vector2(0f, 0f);
            detRt.anchorMax = new Vector2(1f, 0f);
            detRt.pivot = new Vector2(0.5f, 0f);
            detRt.offsetMin = new Vector2(44f, 84f);
            detRt.offsetMax = new Vector2(-44f, 162f);
            Image detBg = detailGo.GetComponent<Image>();
            detBg.sprite = solid;
            detBg.color = new Color(0.06f, 0.07f, 0.10f, 0.90f);
            detBg.raycastTarget = false;

            _detailTitleText = MakeLabel(detRt, "DetailTitle", font, 20f, FontStyles.Bold, new Color(1f, 0.88f, 0.48f, 1f),
                new Vector2(0f, 0.52f), new Vector2(1f, 1f), new Vector2(16f, 0f), new Vector2(-16f, -4f));
            _detailBodyText = MakeLabel(detRt, "DetailBody", font, 16f, FontStyles.Normal, new Color(0.88f, 0.91f, 0.96f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0.54f), new Vector2(16f, 4f), new Vector2(-16f, 0f));

            // Confirm & Cancel Buttons
            _confirmButton = MakeActionButton(winRect, "ConfirmButton", "Confirm Loadout", font, solid,
                new Vector2(-125f, 42f), new Vector2(220f, 48f), new Color(0.20f, 0.62f, 0.36f, 1f), ConfirmAndClose, out _);
            _cancelButton = MakeActionButton(winRect, "CancelButton", "Skip New Relic", font, solid,
                new Vector2(125f, 42f), new Vector2(220f, 48f), new Color(0.42f, 0.24f, 0.26f, 1f), CancelAndClose, out _cancelButtonLabel);

            _modalRoot.SetActive(false);
        }

        private ModalSlot BuildModalSlot(RectTransform parent, string name, RelicDragZone zone, int index, Vector2 size, Sprite solid, TMP_FontAsset font)
        {
            int layer = parent.gameObject.layer;
            GameObject go = new(name, typeof(RectTransform), typeof(Image));
            go.layer = layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            Image borderImg = go.GetComponent<Image>();
            borderImg.sprite = solid;
            borderImg.color = new Color(0.65f, 0.58f, 0.42f, 1f);

            GameObject innerGo = new("Inner", typeof(RectTransform), typeof(Image));
            innerGo.layer = layer;
            RectTransform innerRt = (RectTransform)innerGo.transform;
            innerRt.SetParent(rt, false);
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(4f, 4f);
            innerRt.offsetMax = new Vector2(-4f, -4f);
            Image bgImg = innerGo.GetComponent<Image>();
            bgImg.sprite = solid;
            bgImg.color = new Color(0.16f, 0.17f, 0.22f, 0.94f);
            bgImg.raycastTarget = false;

            GameObject iconGo = new("Icon", typeof(RectTransform), typeof(Image));
            iconGo.layer = layer;
            RectTransform iconRt = (RectTransform)iconGo.transform;
            iconRt.SetParent(innerRt, false);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.64f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(68f, 68f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.enabled = false;

            TextMeshProUGUI mono = MakeLabel(innerRt, "Monogram", font, 28f, FontStyles.Bold, new Color(1f, 0.88f, 0.50f, 1f),
                new Vector2(0f, 0.36f), new Vector2(1f, 0.94f), Vector2.zero, Vector2.zero);
            mono.alignment = TextAlignmentOptions.Center;

            TextMeshProUGUI title = MakeLabel(innerRt, "Title", font, 15.5f, FontStyles.Bold, Color.white,
                new Vector2(0f, 0f), new Vector2(1f, 0.34f), new Vector2(6f, 4f), new Vector2(-6f, 0f));
            title.alignment = TextAlignmentOptions.Center;

            RelicSwapSlotHandler handler = go.AddComponent<RelicSwapSlotHandler>();
            handler.Init(this, zone, index);

            return new ModalSlot
            {
                Zone = zone,
                SlotIndex = index,
                Root = rt,
                Background = bgImg,
                Border = borderImg,
                Icon = iconImg,
                Monogram = mono,
                Title = title
            };
        }

        private static TextMeshProUGUI MakeLabel(
            RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style, Color color,
            Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAlignmentOptions.Left;
            t.raycastTarget = false;
            if (font != null) t.font = font;
            return t;
        }

        private static Button MakeActionButton(
            RectTransform parent, string name, string label, TMP_FontAsset font, Sprite solid,
            Vector2 anchoredPos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI labelOut)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = parent.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            Image bg = go.GetComponent<Image>();
            bg.sprite = solid;
            bg.color = bgColor;
            Button btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);
            labelOut = MakeLabel(rt, "Label", font, 20f, FontStyles.Bold, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            labelOut.alignment = TextAlignmentOptions.Center;
            labelOut.text = label;
            return btn;
        }

        private static RelicData FirstNonNull(List<RelicData> list)
        {
            foreach (RelicData r in list) if (r != null) return r;
            return null;
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            Texture2D tex = Texture2D.whiteTexture;
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _solidSprite;
        }
    }

    internal sealed class RelicSwapSlotHandler : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private RelicSwapModalUI _owner;
        private RelicDragZone _zone;
        private int _index;

        public void Init(RelicSwapModalUI owner, RelicDragZone zone, int index)
        {
            _owner = owner;
            _zone = zone;
            _index = index;
        }

        public void OnPointerEnter(PointerEventData eventData) => _owner?.OnSlotHoverEnter(_zone, _index);
        public void OnPointerExit(PointerEventData eventData) => _owner?.OnSlotHoverExit(_zone, _index);
        public void OnBeginDrag(PointerEventData eventData) => _owner?.OnSlotBeginDrag(_zone, _index, eventData);
        public void OnDrag(PointerEventData eventData) => _owner?.OnSlotDrag(eventData);
        public void OnEndDrag(PointerEventData eventData) => _owner?.OnSlotEndDrag();
        public void OnDrop(PointerEventData eventData) => _owner?.OnSlotDrop(_zone, _index);
    }
}

