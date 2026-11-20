using System;
using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Inputs;
using PlushieOdyssey.UI.Combat;
using PlushieOdyssey.UI.Relics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PlushieOdyssey.Combat.Views
{
    [DisallowMultipleComponent]
    public class CombatHandController : MonoBehaviour, IBackHandler
    {
        [Header("Presentation")]
        [SerializeField] private HandView _handView;
        [SerializeField] private CardViewHoverSystem _hoverPreview;
        [Tooltip("Disabled by default so hovering elevates the card in hand and spreads neighbors instead of duplicating a ghost card.")]
        [SerializeField] private bool _useHoverPreview = false;
        [SerializeField] private RectTransform _dragLayer;
        [SerializeField] private RectTransform _playRegion;
        [SerializeField] private Canvas _canvas;

        [Header("Audio")]
        [SerializeField] private SfxID _fallbackHoverSfx = SfxID.hover;
        [SerializeField] private SfxID _pickupSfx = SfxID.card_pickup;
        [SerializeField] private SfxID _targetStartSfx = SfxID.card_target_start;
        [SerializeField] private SfxID _rejectedSfx = SfxID.card_release_invalid;

        [Header("Optional Integration")]
        [SerializeField] private CardPlayService _playService;
        [SerializeField] private InputContextRouter _inputRouter;

        private sealed class Binding
        {
            public CardView View;
            public CardPointerInput Input;
            public CardVisualFeedback Feedback;
        }

        private readonly Dictionary<CardPointerInput, Binding> _bindings = new();
        private Binding _hovered;
        private Binding _active;
        private Vector2 _hoverPosition;
        private bool _targeting;
        private Transform _originalParent;
        private Vector2 _originalAnchorMin;
        private Vector2 _originalAnchorMax;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private Vector3 _originalScale;
        private int _originalSibling;
        private Vector2 _dragOffset;
        private CanvasGroup _dragGroup;
        private bool _originalBlocksRaycasts;
        private IDisposable _contextLease;
        private RelicSwapModalUI _relicSwapModal;
        private Vector2? _simulatedPointerPos;

        public bool IsCardInteracting => _active != null || _targeting;
        public CardView HoveredCard => _hovered?.View;

        public void SetSimulatedPointerPosition(Vector2? screenPos) => _simulatedPointerPos = screenPos;

        public event Action<CardView, CombatantView> PlayRequested;
        public event Action<CardView> PlayAccepted;
        public event Action<CardView, CardPlayFailure> PlayRejected;
        public event Action<CardView> InteractionCancelled;
        public event Action<CardView, Vector2> TargetingChanged;
        public event Action TargetingEnded;
        public event Action<CardView, Vector2> HoverCardChanged;
        public event Action HoverCardEnded;
        public event Action<CardView, Vector2, bool> CardDragUpdated;
        public event Action CardDragEnded;

        private bool IsModalBlocking =>
            CombatMapOverlayUI.IsAnyOpen ||
            CombatPileViewerUI.IsAnyOpen ||
            (_relicSwapModal != null && _relicSwapModal.IsOpen) ||
            Time.timeScale == 0f;

        private bool CanReceiveInput => isActiveAndEnabled &&
            !IsModalBlocking &&
            (_inputRouter == null || _inputRouter.CurrentContext == InputContext.Combat) &&
            (_playService == null || !_playService.IsTurnLocked);

        private bool CanHoverCards => isActiveAndEnabled &&
            !IsModalBlocking &&
            (_inputRouter == null || _inputRouter.CurrentContext == InputContext.Combat) &&
            (_playService == null || !_playService.IsTurnLocked);

        private Camera EventCamera => _canvas != null &&
            _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        private void OnEnable()
        {
            if (_handView == null)
            {
                Debug.LogWarning("Assign HandView to CombatHandController.", this);
                return;
            }

            _handView.CardAdded += RegisterCard;
            _handView.CardRemoved += UnregisterCard;
            foreach (CardView card in _handView.Cards) RegisterCard(card);
            if (_playService != null) _playService.StateChanged += OnServiceStateChanged;
            if (_inputRouter == null) _inputRouter = InputContextRouter.FromBootstrap;
            if (_relicSwapModal == null) _relicSwapModal = FindFirstObjectByType<RelicSwapModalUI>();
            if (_inputRouter != null && _inputRouter.isActiveAndEnabled)
                _contextLease = _inputRouter.EnterContext(this, InputContext.Combat, this);
            RefreshCardStates();
        }

        private void OnDisable()
        {
            CancelInteraction();
            ClearHover(false);
            if (_handView != null)
            {
                _handView.CardAdded -= RegisterCard;
                _handView.CardRemoved -= UnregisterCard;
            }
            if (_playService != null) _playService.StateChanged -= OnServiceStateChanged;
            foreach (Binding binding in _bindings.Values) Unsubscribe(binding);
            _bindings.Clear();
            _contextLease?.Dispose();
            _contextLease = null;
        }

        private void LateUpdate()
        {
            if (!CanReceiveInput && _active != null)
            {
                CancelInteraction();
            }

            if (!CanHoverCards)
            {
                if (_hovered != null) ClearHover();
                return;
            }

            // Continuously reconcile hovered card under cursor so playing a card or shifting the hand
            // immediately hovers the next card under a stationary cursor (no need to hover out & back!).
            if (_active == null)
            {
                SyncHoverUnderPointer();
            }
        }

        private void SyncHoverUnderPointer()
        {
            if (_handView == null || _handView.Cards == null || _handView.Cards.Count == 0)
            {
                if (_hovered != null) ClearHover();
                return;
            }

            Vector2 pointerPos;
            if (_simulatedPointerPos.HasValue)
            {
                pointerPos = _simulatedPointerPos.Value;
            }
            else if (Mouse.current != null)
            {
                pointerPos = Mouse.current.position.ReadValue();
            }
            else
            {
                return;
            }

            // Keep currently hovered card sticky while pointer remains inside its unmoving HoverHitbox
            // OR its lifted Wrapper visual so moving the mouse onto the elevated card never flickers off.
            if (_hovered != null && _hovered.View != null && _hovered.View.HasContent &&
                IsPointerOverCard(_hovered.View, pointerPos, includeElevatedVisual: true))
            {
                _hoverPosition = pointerPos;
                return;
            }

            // Pick top-most card (highest sibling index) under the cursor
            Binding best = null;
            int bestSibling = int.MinValue;

            foreach (Binding binding in _bindings.Values)
            {
                if (binding?.View == null || !binding.View.HasContent || !binding.View.gameObject.activeInHierarchy)
                    continue;

                if (!IsPointerOverCard(binding.View, pointerPos, includeElevatedVisual: false)) continue;

                int sibling = binding.View.transform.GetSiblingIndex();
                if (sibling > bestSibling)
                {
                    bestSibling = sibling;
                    best = binding;
                }
            }

            if (best == _hovered) return;

            if (best != null)
            {
                ApplyHover(best, pointerPos);
            }
            else if (_hovered != null)
            {
                ClearHover();
            }
        }

        private bool IsPointerOverCard(CardView view, Vector2 screenPos, bool includeElevatedVisual)
        {
            if (view == null || view.transform is not RectTransform cardRect) return false;
            RectTransform hitbox = cardRect.Find("HoverHitbox") as RectTransform;
            RectTransform targetRect = hitbox != null ? hitbox : cardRect;
            if (RectTransformUtility.RectangleContainsScreenPoint(targetRect, screenPos, EventCamera))
                return true;

            if (includeElevatedVisual)
            {
                RectTransform wrapper = cardRect.Find("Wrapper") as RectTransform;
                if (wrapper != null && RectTransformUtility.RectangleContainsScreenPoint(wrapper, screenPos, EventCamera))
                    return true;
            }

            return false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            CancelInteraction();
            ClearHover();
        }

        public bool TryHandleBack()
        {
            if (_active == null) return false;
            CancelInteraction();
            return true;
        }

        public void CancelInteraction()
        {
            if (_active == null) return;
            CardView view = _active.View;
            EndInteraction();
            InteractionCancelled?.Invoke(view);
        }

        public void RefreshCardStates()
        {
            foreach (Binding binding in _bindings.Values)
            {
                CardView view = binding.View;
                if (view == null || !view.HasContent) continue;
                if (_playService == null)
                {
                    view.SetBlocked(false);
                    continue;
                }

                view.SetDisplayedCost(_playService.GetEffectiveManaCost(view.Card));
                view.SetBlocked(_playService.GetFailure(view.Card, requireTarget: false) != CardPlayFailure.None);
            }

            if (_useHoverPreview && _hovered != null && _hovered.View != null && _hoverPreview != null)
                _hoverPreview.Show(_hovered.View.DisplayData, _hoverPosition, _hovered.View.IsBlocked);
        }

        private void RegisterCard(CardView view)
        {
            if (view == null) return;
            if (!view.TryGetComponent(out CardPointerInput input))
            {
                Debug.LogWarning("Add CardPointerInput to the hand card's root.", view);
                return;
            }
            if (_bindings.ContainsKey(input)) return;

            var binding = new Binding
            {
                View = view,
                Input = input,
                Feedback = view.GetComponent<CardVisualFeedback>()
            };
            // Disable child menu Buttons (e.g., Wrapper) so pointer/drag events route to CardPointerInput.
            foreach (UnityEngine.UI.Button childButton in view.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (childButton != null) childButton.enabled = false;
            }
            EnsureHoverHitbox(view);
            _bindings.Add(input, binding);
            input.HoverEntered += OnHoverEntered;
            input.HoverExited += OnHoverExited;
            input.Pressed += OnPressed;
            input.Released += OnReleased;
            input.Dragged += OnDragged;
            input.Cancelled += OnInputCancelled;
            RefreshCardStates();
        }

        private void UnregisterCard(CardView view)
        {
            if (view == null || !view.TryGetComponent(out CardPointerInput input) ||
                !_bindings.TryGetValue(input, out Binding binding)) return;
            if (_active == binding) CancelInteraction();
            if (_hovered == binding) ClearHover();
            Unsubscribe(binding);
            _bindings.Remove(input);
        }

        private void Unsubscribe(Binding binding)
        {
            if (binding.Feedback != null) binding.Feedback.ResetFeedback();
            if (binding.Input == null) return;
            binding.Input.HoverEntered -= OnHoverEntered;
            binding.Input.HoverExited -= OnHoverExited;
            binding.Input.Pressed -= OnPressed;
            binding.Input.Released -= OnReleased;
            binding.Input.Dragged -= OnDragged;
            binding.Input.Cancelled -= OnInputCancelled;
        }

        private void OnHoverEntered(CardPointerInput input, PointerEventData eventData)
        {
            if (!CanHoverCards || _active != null ||
                !_bindings.TryGetValue(input, out Binding binding) || !binding.View.HasContent) return;
            ApplyHover(binding, eventData.position);
        }

        private void ApplyHover(Binding binding, Vector2 pointerPosition)
        {
            if (binding == null || _hovered == binding) return;
            ClearHover(false);
            _hovered = binding;
            _hoverPosition = pointerPosition;
            binding.View.transform.SetAsLastSibling();
            if (_handView != null) _handView.SetHoveredCard(binding.View);
            if (binding.Feedback != null) binding.Feedback.SetHovered(true);
            if (_useHoverPreview && _hoverPreview != null)
                _hoverPreview.Show(binding.View.DisplayData, _hoverPosition, binding.View.IsBlocked);
            PlayHoverAudio(binding.View);
            HoverCardChanged?.Invoke(binding.View, _hoverPosition);
        }

        private void OnHoverExited(CardPointerInput input, PointerEventData eventData)
        {
            if (_hovered != null && _hovered.Input == input) ClearHover();
        }

        private void ClearHover(bool restoreOrder = true)
        {
            Binding hovered = _hovered;
            _hovered = null;
            if (hovered?.Feedback != null) hovered.Feedback.SetHovered(false);
            if (_hoverPreview != null) _hoverPreview.Hide();
            if (hovered != null) HoverCardEnded?.Invoke();
            if (restoreOrder && isActiveAndEnabled && _handView != null)
            {
                _handView.SetHoveredCard(null);
                _handView.RefreshCardOrder();
            }
        }

        private void OnPressed(CardPointerInput input, PointerEventData eventData)
        {
            if (!CanReceiveInput || _active != null ||
                !_bindings.TryGetValue(input, out Binding binding) ||
                binding.View.transform is not RectTransform rect || !binding.View.HasContent) return;

            CardPlayFailure failure = _playService != null
                ? _playService.GetFailure(binding.View.Card, requireTarget: false)
                : CardPlayFailure.None;
            if (failure != CardPlayFailure.None)
            {
                PlaySfx(_rejectedSfx);
                PlayRejected?.Invoke(binding.View, failure);
                return;
            }

            bool targeting = binding.View.Card?.ManualTargetEffect != null;
            if (!targeting && !TryGetDragPosition(eventData.position, out _))
            {
                Debug.LogWarning("Assign Canvas and a Drag Layer with CanvasGroup.", this);
                return;
            }

            ClearHover();
            _active = binding;
            _targeting = targeting;
            rect.DOKill();
            rect.localScale = Vector3.one;
            _originalParent = rect.parent;
            _originalAnchorMin = rect.anchorMin;
            _originalAnchorMax = rect.anchorMax;
            _originalPosition = rect.anchoredPosition3D;
            _originalRotation = rect.localRotation;
            _originalScale = Vector3.one;
            _originalSibling = rect.GetSiblingIndex();
            _handView.SuspendCardLayout(binding.View);
            binding.View.SetSelected(true);
            PlaySfx(_pickupSfx);

            if (targeting)
            {
                rect.localRotation = Quaternion.identity;
                rect.SetAsLastSibling();
                if (binding.Feedback != null) binding.Feedback.SetTargeted(true);
                PlaySfx(_targetStartSfx);
                TargetingChanged?.Invoke(binding.View, eventData.position);
                return;
            }

            if (binding.Feedback != null) binding.Feedback.SetPressed(true);

            _dragGroup = _dragLayer.GetComponent<CanvasGroup>();
            _originalBlocksRaycasts = _dragGroup.blocksRaycasts;
            _dragGroup.blocksRaycasts = false;
            rect.SetParent(_dragLayer, true);
            rect.anchorMin = rect.anchorMax = _dragLayer.pivot;
            rect.localRotation = Quaternion.identity;
            rect.SetAsLastSibling();
            TryGetDragPosition(eventData.position, out Vector2 pointer);
            _dragOffset = pointer - (Vector2)rect.localPosition;
            MoveDraggedCard(eventData.position);
            CardDragUpdated?.Invoke(binding.View, eventData.position, IsInsidePlayRegion(eventData.position));
        }

        private void OnDragged(CardPointerInput input, PointerEventData eventData)
        {
            if (_active == null || _active.Input != input) return;
            if (!CanReceiveInput)
            {
                CancelInteraction();
                return;
            }
            if (_targeting) TargetingChanged?.Invoke(_active.View, eventData.position);
            else
            {
                MoveDraggedCard(eventData.position);
                CardDragUpdated?.Invoke(_active.View, eventData.position, IsInsidePlayRegion(eventData.position));
            }
        }

        private void OnReleased(CardPointerInput input, PointerEventData eventData)
        {
            if (_active == null || _active.Input != input) return;
            CardView view = _active.View;
            bool targeting = _targeting;
            bool validRegion = CanReceiveInput && !IsBlockedByUI(eventData) &&
                (targeting || (_playRegion != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(_playRegion, eventData.position, EventCamera)));
            CombatantView target = null;
            if (validRegion && targeting && _playService != null)
                _playService.TryResolveTarget(view.Card, eventData.position, out target);

            // Clear interaction before the backend can synchronously remove the card.
            EndInteraction();
            if (!validRegion)
            {
                PlaySfx(_rejectedSfx);
                PlayRejected?.Invoke(view, CardPlayFailure.InvalidDrop);
                return;
            }

            PlayRequested?.Invoke(view, target);
            CardPlayFailure failure = CardPlayFailure.MissingBackend;
            if (_playService != null && _playService.TryPlay(view.Card, target, out failure))
                PlayAccepted?.Invoke(view);
            else
            {
                PlaySfx(_rejectedSfx);
                PlayRejected?.Invoke(view, failure);
            }
            RefreshCardStates();
        }

        private void OnInputCancelled(CardPointerInput input)
        {
            if (_active != null && _active.Input == input) CancelInteraction();
            if (_hovered != null && _hovered.Input == input) ClearHover();
        }

        private void OnServiceStateChanged()
        {
            if (_active != null && _playService != null &&
                _playService.GetFailure(_active.View.Card, requireTarget: false) != CardPlayFailure.None)
                CancelInteraction();
            if (_playService != null && _playService.IsTurnLocked) ClearHover();
            RefreshCardStates();
        }

        private void EndInteraction()
        {
            Binding binding = _active;
            bool targeting = _targeting;
            _active = null;
            _targeting = false;
            if (_dragGroup != null) _dragGroup.blocksRaycasts = _originalBlocksRaycasts;
            _dragGroup = null;
            if (binding?.Input != null) binding.Input.Cancel();

            if (binding?.View != null)
            {
                if (Application.isPlaying && binding.View.gameObject.activeInHierarchy &&
                    binding.View.transform is RectTransform rect && _originalParent != null &&
                    _originalParent.gameObject.activeInHierarchy)
                {
                    rect.SetParent(_originalParent, false);
                    rect.anchorMin = _originalAnchorMin;
                    rect.anchorMax = _originalAnchorMax;
                    rect.anchoredPosition3D = _originalPosition;
                    rect.localRotation = _originalRotation;
                    rect.localScale = _originalScale;
                    rect.SetSiblingIndex(_originalSibling);
                }
                binding.View.SetSelected(false);
                if (binding.Feedback != null) binding.Feedback.ResetFeedback();
                if (_handView != null) _handView.ResumeCardLayout(binding.View);
            }
            if (_hoverPreview != null) _hoverPreview.Hide();
            if (targeting) TargetingEnded?.Invoke();
            else if (binding != null) CardDragEnded?.Invoke();
        }

        private bool IsInsidePlayRegion(Vector2 screenPosition)
        {
            return _playRegion != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_playRegion, screenPosition, EventCamera);
        }

        private void PlayHoverAudio(CardView view)
        {
            SfxID id = SfxID.None;
            if (view != null && view.Card != null && view.Card.HoverSfx != SfxID.None)
                id = view.Card.HoverSfx;
            else if (view != null && view.CardData != null && view.CardData.HoverSfx != SfxID.None)
                id = view.CardData.HoverSfx;
            else
                id = _fallbackHoverSfx;

            PlaySfx(id);
        }

        private static void PlaySfx(SfxID id)
        {
            if (id != SfxID.None) Tune.SFX(id);
        }

        private static void EnsureHoverHitbox(CardView view)
        {
            if (view == null || view.transform is not RectTransform cardRect) return;
            if (cardRect.Find("HoverHitbox") != null) return;

            RectTransform wrapper = cardRect.Find("Wrapper") as RectTransform;
            Vector2 size = wrapper != null && wrapper.rect.width > 10f && wrapper.rect.height > 10f
                ? wrapper.rect.size
                : new Vector2(270f, 340f);

            GameObject hitboxGo = new("HoverHitbox", typeof(RectTransform), typeof(Image));
            hitboxGo.layer = view.gameObject.layer;
            RectTransform hitboxRect = (RectTransform)hitboxGo.transform;
            hitboxRect.SetParent(cardRect, false);
            hitboxRect.SetAsFirstSibling();
            hitboxRect.anchorMin = hitboxRect.anchorMax = new Vector2(0.5f, 0.5f);
            hitboxRect.pivot = new Vector2(0.5f, 0.5f);
            hitboxRect.anchoredPosition = Vector2.zero;
            hitboxRect.sizeDelta = size;

            Image img = hitboxGo.GetComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true;
        }

        private bool TryGetDragPosition(Vector2 screenPosition, out Vector2 localPosition)
        {
            localPosition = default;
            if (_canvas == null || _dragLayer == null || _dragLayer.GetComponent<CanvasGroup>() == null)
                return false;
            if (_canvas.renderMode != RenderMode.ScreenSpaceOverlay && _canvas.worldCamera == null)
                return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _dragLayer, screenPosition, EventCamera, out localPosition);
        }

        private void MoveDraggedCard(Vector2 screenPosition)
        {
            if (_active?.View == null || !TryGetDragPosition(screenPosition, out Vector2 position)) return;
            RectTransform rect = (RectTransform)_active.View.transform;
            position -= _dragOffset;
            rect.anchoredPosition3D = new Vector3(position.x, position.y, 0f);
        }

        private bool IsBlockedByUI(PointerEventData eventData)
        {
            RaycastResult hit = eventData.pointerCurrentRaycast;
            if (hit.module is not UnityEngine.UI.GraphicRaycaster || hit.gameObject == null) return false;
            Canvas hitCanvas = hit.gameObject.GetComponentInParent<Canvas>();
            if (hitCanvas != null && hitCanvas.rootCanvas.renderMode == RenderMode.WorldSpace) return false;
            return _playRegion == null || !hit.gameObject.transform.IsChildOf(_playRegion);
        }
    }
}
