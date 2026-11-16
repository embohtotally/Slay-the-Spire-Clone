using System;
using System.Collections.Generic;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Inputs;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlushieOdyssey.Combat.Views
{
    [DisallowMultipleComponent]
    public class CombatHandController : MonoBehaviour, IBackHandler
    {
        [Header("Presentation")]
        [SerializeField] private HandView _handView;
        [SerializeField] private CardViewHoverSystem _hoverPreview;
        [SerializeField] private RectTransform _dragLayer;
        [SerializeField] private RectTransform _playRegion;
        [SerializeField] private Canvas _canvas;

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

        public event Action<CardView, CombatantView> PlayRequested;
        public event Action<CardView> PlayAccepted;
        public event Action<CardView, CardPlayFailure> PlayRejected;
        public event Action<CardView> InteractionCancelled;
        public event Action<CardView, Vector2> TargetingChanged;
        public event Action TargetingEnded;

        private bool CanReceiveInput => isActiveAndEnabled &&
            (_inputRouter == null || _inputRouter.CurrentContext == InputContext.Combat) &&
            (_playService == null || !_playService.IsBusy);

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
            if (!CanReceiveInput && (_active != null || _hovered != null))
            {
                CancelInteraction();
                ClearHover();
            }
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

            if (_hovered != null && _hovered.View != null && _hoverPreview != null)
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
            if (!CanReceiveInput || _active != null ||
                !_bindings.TryGetValue(input, out Binding binding) || !binding.View.HasContent) return;
            ClearHover();
            _hovered = binding;
            _hoverPosition = eventData.position;
            binding.View.transform.SetAsLastSibling();
            if (binding.Feedback != null) binding.Feedback.SetHovered(true);
            if (_hoverPreview != null)
                _hoverPreview.Show(binding.View.DisplayData, _hoverPosition, binding.View.IsBlocked);
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
            if (restoreOrder && isActiveAndEnabled && _handView != null)
                _handView.RefreshCardOrder();
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
            _originalParent = rect.parent;
            _originalAnchorMin = rect.anchorMin;
            _originalAnchorMax = rect.anchorMax;
            _originalPosition = rect.anchoredPosition3D;
            _originalRotation = rect.localRotation;
            _originalScale = rect.localScale;
            _originalSibling = rect.GetSiblingIndex();
            _handView.SuspendCardLayout(binding.View);
            binding.View.SetSelected(true);
            if (binding.Feedback != null) binding.Feedback.SetPressed(true);

            if (targeting)
            {
                TargetingChanged?.Invoke(binding.View, eventData.position);
                return;
            }

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
            else MoveDraggedCard(eventData.position);
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
                PlayRejected?.Invoke(view, CardPlayFailure.InvalidDrop);
                return;
            }

            PlayRequested?.Invoke(view, target);
            CardPlayFailure failure = CardPlayFailure.MissingBackend;
            if (_playService != null && _playService.TryPlay(view.Card, target, out failure))
                PlayAccepted?.Invoke(view);
            else
                PlayRejected?.Invoke(view, failure);
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
            if (_playService != null && _playService.IsBusy) ClearHover();
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
            return _playRegion == null || !hit.gameObject.transform.IsChildOf(_playRegion);
        }
    }
}
