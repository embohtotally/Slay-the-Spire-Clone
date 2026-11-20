using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlushieOdyssey.Combat.Views
{
    [DisallowMultipleComponent]
    public class CardPointerInput : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private bool _interactable = true;

        private int? _pressedPointerId;
        [SerializeField, ReadOnly] private bool _hovered;

        public event Action<CardPointerInput, PointerEventData> HoverEntered;
        public event Action<CardPointerInput, PointerEventData> HoverExited;
        public event Action<CardPointerInput, PointerEventData> Pressed;
        public event Action<CardPointerInput, PointerEventData> Released;
        public event Action<CardPointerInput, PointerEventData> Dragged;
        public event Action<CardPointerInput> Cancelled;

        [ShowNativeProperty]
        public int PressedPointerId => _pressedPointerId ?? -99;

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            if (!interactable) Cancel();
        }

        public void Cancel()
        {
            _pressedPointerId = null;
            _hovered = false;
            Cancelled?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactable || !isActiveAndEnabled) return;
            _hovered = true;
            HoverEntered?.Invoke(this, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_hovered) return;
            _hovered = false;
            HoverExited?.Invoke(this, eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable || !isActiveAndEnabled || _pressedPointerId.HasValue ||
                eventData.button != PointerEventData.InputButton.Left) return;

            _pressedPointerId = eventData.pointerId;
            Pressed?.Invoke(this, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_pressedPointerId != eventData.pointerId ||
                eventData.button != PointerEventData.InputButton.Left) return;

            _pressedPointerId = null;
            Released?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_interactable || !isActiveAndEnabled || _pressedPointerId != eventData.pointerId) return;
            Dragged?.Invoke(this, eventData);
        }

        private void OnDisable() => Cancel();
    }
}
