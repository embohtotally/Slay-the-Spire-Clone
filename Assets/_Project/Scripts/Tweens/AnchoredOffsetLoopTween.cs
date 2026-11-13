using DG.Tweening;
using UnityEngine;

namespace PlushieOdyssey.Tweens
{
    /// <summary>Moves one or two arrows together, returning via an infinite yoyo loop.</summary>
    public class AnchoredOffsetLoopTween : BaseTween
    {
        [SerializeField] private Vector2 _offset = new(-20f, 0f);
        [SerializeField] private RectTransform _secondTarget = null;
        [SerializeField] private Vector2 _secondOffset = new(20f, 0f);

        protected override bool SupportsRelativeAndSpeedBased => false;

        protected override void ResetTweenSettings()
        {
            base.ResetTweenSettings();
            ConfigureBaseSettings(Ease.InOutQuad, -1, LoopType.Yoyo, independentUpdate: true);
        }

        protected override Tween CreateTween()
        {
            Vector2 start = Target.anchoredPosition;
            Vector2 secondStart = _secondTarget != null ? _secondTarget.anchoredPosition : Vector2.zero;
            float progress = 0f;

            return DOTween.To(() => progress, value =>
            {
                progress = value;
                if (Target != null) Target.anchoredPosition = start + _offset * value;
                if (_secondTarget != null) _secondTarget.anchoredPosition = secondStart + _secondOffset * value;
            }, 1f, Duration);
        }
    }
}
