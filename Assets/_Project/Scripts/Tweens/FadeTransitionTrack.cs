using System;
using DG.Tweening;
using UnityEngine;

namespace PlushieOdyssey.Tweens
{
    /// <summary>Opacity only; never changes interaction or raycast settings.</summary>
    [Serializable]
    public sealed class FadeTransitionTrack
    {
        public CanvasGroup Target = null;
        [Range(0f, 1f)] public float OpenAlpha = 1f;
        [Range(0f, 1f)] public float ClosedAlpha = 0f;
        public TransitionTrackTiming Timing = new();

        public Tween CreateTween(bool opening, bool enterFromOffset, float duration, Ease easing)
        {
            if (Target == null) return null;

            CanvasGroup target = Target;
            float current = target.alpha;
            float from = opening ? (enterFromOffset ? ClosedAlpha : current) : ClosedAlpha;
            float to = opening ? OpenAlpha : current;
            if (opening && enterFromOffset) target.alpha = from;

            float progress = 0f;
            return DOTween.To(() => progress, value =>
            {
                progress = value;
                if (target != null) target.alpha = Mathf.LerpUnclamped(from, to, value);
            }, 1f, Timing?.ResolveDuration(duration) ?? Mathf.Max(0.001f, duration))
                .SetEase(Timing?.ResolveEase(easing) ?? easing);
        }

        public void ApplyInstant(bool open)
        {
            if (Target != null) Target.alpha = open ? OpenAlpha : ClosedAlpha;
        }
    }
}
