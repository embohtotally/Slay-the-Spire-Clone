using System;
using DG.Tweening;
using UnityEngine;

namespace PlushieOdyssey.Tweens
{
    /// <summary>Visual-only slide data. Playback, input and visibility belong to its owner.</summary>
    [Serializable]
    public sealed class SlideTransitionTrack
    {
        // Keep these field names stable: existing _additionalParts serialize them inline.
        public RectTransform Target = null;
        public Vector2 OpenPosition = Vector2.zero;
        public Vector2 OpenStartOffset = new(2000f, 0f);
        public Vector2 ClosedOffset = new(-2000f, 0f);
        public TransitionTrackTiming Timing = new();

        public Tween CreateTween(bool opening, bool enterFromOffset, float duration, Ease easing)
        {
            if (Target == null) return null;

            RectTransform target = Target;
            // Both timelines use closed (0) -> open/current (1). The owner plays exit backwards.
            Vector2 from = opening
                ? (enterFromOffset ? OpenPosition + OpenStartOffset : target.anchoredPosition)
                : OpenPosition + ClosedOffset;
            Vector2 to = opening ? OpenPosition : target.anchoredPosition;
            // Delayed entrance tracks must start hidden, not pop offscreen when their delay ends.
            if (opening && enterFromOffset) target.anchoredPosition = from;

            float progress = 0f;
            return DOTween.To(() => progress, value =>
            {
                progress = value;
                if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, value);
            }, 1f, Timing?.ResolveDuration(duration) ?? Mathf.Max(0.001f, duration))
                .SetEase(Timing?.ResolveEase(easing) ?? easing);
        }

        public void ApplyInstant(bool open)
        {
            if (Target != null) Target.anchoredPosition = OpenPosition + (open ? Vector2.zero : ClosedOffset);
        }
    }
}
