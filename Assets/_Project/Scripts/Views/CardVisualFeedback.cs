using DG.Tweening;
using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    [DisallowMultipleComponent]
    public class CardVisualFeedback : MonoBehaviour
    {
        [Tooltip("Inner visual root; do not assign the root moved by HandView.")]
        [SerializeField] private RectTransform _visualRoot;
        [SerializeField, Min(0f)] private float _hoverScale = 1.22f;
        [SerializeField] private float _hoverLiftY = 122f;
        [SerializeField, Min(0f)] private float _targetedScale = 1.12f;
        [SerializeField] private float _targetedLiftY = 72f;
        [SerializeField, Min(0f)] private float _pressedScale = 1.02f;
        [SerializeField, Min(0f)] private float _duration = 0.14f;
        [SerializeField] private Ease _ease = Ease.OutCubic;

        private Vector3 _baseScale;
        private Vector2 _basePosition;
        private bool _initialized;
        private bool _hovered;
        private bool _pressed;
        private bool _targeted;
        private Tween _scaleTween;
        private Tween _moveTween;

        private void Awake() => Initialize();

        public void SetHovered(bool hovered)
        {
            _hovered = hovered;
            Refresh();
        }

        public void SetPressed(bool pressed)
        {
            _pressed = pressed;
            if (!pressed) _targeted = false;
            Refresh();
        }

        public void SetTargeted(bool targeted)
        {
            _targeted = targeted;
            Refresh();
        }

        public void ResetFeedback()
        {
            _hovered = false;
            _pressed = false;
            _targeted = false;
            KillTweens();
            if (_initialized && _visualRoot != null)
            {
                _visualRoot.localScale = _baseScale;
                _visualRoot.anchoredPosition = _basePosition;
            }
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
            _basePosition = _visualRoot.anchoredPosition;
            _initialized = true;
        }

        private void Refresh()
        {
            Initialize();
            if (!_initialized || !isActiveAndEnabled) return;
            KillTweens();

            float scale = 1f;
            float liftY = 0f;

            if (_targeted)
            {
                scale = _targetedScale;
                liftY = _targetedLiftY;
            }
            else if (_pressed)
            {
                scale = _pressedScale;
                liftY = 0f;
            }
            else if (_hovered)
            {
                scale = _hoverScale;
                liftY = _hoverLiftY;
            }

            Vector2 targetPos = new Vector2(_basePosition.x, _basePosition.y + liftY);
            Vector3 targetScale = _baseScale * scale;

            if (!Application.isPlaying || _duration == 0f)
            {
                _visualRoot.localScale = targetScale;
                _visualRoot.anchoredPosition = targetPos;
                return;
            }

            _scaleTween = _visualRoot.DOScale(targetScale, _duration)
                .SetEase(_ease)
                .SetUpdate(true)
                .SetRecyclable(false)
                .OnKill(() => _scaleTween = null);

            _moveTween = _visualRoot.DOAnchorPos(targetPos, _duration)
                .SetEase(_ease)
                .SetUpdate(true)
                .SetRecyclable(false)
                .OnKill(() => _moveTween = null);
        }

        private void KillTweens()
        {
            _scaleTween?.Kill();
            _scaleTween = null;
            _moveTween?.Kill();
            _moveTween = null;
        }

        private void OnDisable() => ResetFeedback();
    }
}
