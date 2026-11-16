using DG.Tweening;
using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    [DisallowMultipleComponent]
    public class CardVisualFeedback : MonoBehaviour
    {
        [Tooltip("Inner visual root; do not assign the root moved by HandView.")]
        [SerializeField] private RectTransform _visualRoot;
        [SerializeField, Min(0f)] private float _hoverScale = 1.08f;
        [SerializeField, Min(0f)] private float _pressedScale = 0.97f;
        [SerializeField, Min(0f)] private float _duration = 0.12f;
        [SerializeField] private Ease _ease = Ease.OutQuad;

        private Vector3 _baseScale;
        private bool _initialized;
        private bool _hovered;
        private bool _pressed;
        private Tween _scaleTween;

        private void Awake() => Initialize();

        public void SetHovered(bool hovered)
        {
            _hovered = hovered;
            Refresh();
        }

        public void SetPressed(bool pressed)
        {
            _pressed = pressed;
            Refresh();
        }

        public void ResetFeedback()
        {
            _hovered = false;
            _pressed = false;
            _scaleTween?.Kill();
            _scaleTween = null;
            if (_initialized && _visualRoot != null) _visualRoot.localScale = _baseScale;
        }

        private void Initialize()
        {
            if (_initialized || _visualRoot == null) return;
            if (_visualRoot == transform)
            {
                Debug.LogWarning("Assign an inner VisualRoot to CardVisualFeedback.", this);
                return;
            }
            _baseScale = _visualRoot.localScale;
            _initialized = true;
        }

        private void Refresh()
        {
            Initialize();
            if (!_initialized || !isActiveAndEnabled) return;
            _scaleTween?.Kill();

            float scale = _pressed ? _pressedScale : _hovered ? _hoverScale : 1f;
            if (!Application.isPlaying || _duration == 0f)
            {
                _visualRoot.localScale = _baseScale * scale;
                return;
            }

            _scaleTween = _visualRoot.DOScale(_baseScale * scale, _duration)
                .SetEase(_ease)
                .SetUpdate(true)
                .SetRecyclable(false)
                .OnKill(() => _scaleTween = null);
        }

        private void OnDisable() => ResetFeedback();
    }
}
