using DG.Tweening;
using UnityEngine;

namespace PlushieOdyssey.Tweens
{
    /// <summary>Forward fades From Alpha -> To Alpha; reverse fades back.</summary>
    public class CanvasGroupFadeTween : BaseTween
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField, Range(0f, 1f)] private float _fromAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float _toAlpha = 0f;
        [Tooltip("Enable for a screen-covering fade. Leave off for a purely decorative fade.")]
        [SerializeField] private bool _manageInput = false;
        [SerializeField] private bool _interactableWhenVisible = false;

        protected override bool SupportsRelativeAndSpeedBased => false;

        protected override void ResetTweenSettings()
        {
            base.ResetTweenSettings();
            ConfigureBaseSettings(Ease.InOutQuad, independentUpdate: true);
        }

        protected override bool InitializeTween()
        {
            if (_canvasGroup == null) _canvasGroup = Target.GetComponent<CanvasGroup>();
            if (_canvasGroup != null) return true;

            Debug.LogError("[CanvasGroupFadeTween] Assign a CanvasGroup or add one to the Target.", this);
            return false;
        }

        protected override Tween CreateTween()
        {
            return _canvasGroup.DOFade(_toAlpha, Duration).From(_fromAlpha, false);
        }

        protected override void OnPlaybackStarted(bool reverse)
        {
            if (!_manageInput) return;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = true;
        }

        protected override void OnPlaybackCompleted(bool reverse)
        {
            UpdateInputState();
        }

        protected override void OnPoseSnapped(bool atEnd)
        {
            UpdateInputState();
        }

        private void UpdateInputState()
        {
            if (!_manageInput) return;
            bool visible = _canvasGroup.alpha > 0.001f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible && _interactableWhenVisible;
        }
    }
}
