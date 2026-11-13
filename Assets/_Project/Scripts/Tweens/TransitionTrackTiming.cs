using System;
using DG.Tweening;
using UnityEngine;

namespace PlushieOdyssey.Tweens
{
    /// <summary>Local track timing; zero duration inherits the controller's duration.</summary>
    [Serializable]
    public sealed class TransitionTrackTiming
    {
        [Min(0f)] public float Delay = 0f;
        [Tooltip("Zero uses the controller's Duration.")]
        [Min(0f)] public float Duration = 0f;
        public bool OverrideEase = false;
        public Ease Easing = Ease.InOutQuad;

        public float StartDelay => Mathf.Max(0f, Delay);
        public float ResolveDuration(float fallback) => Mathf.Max(0.001f, Duration > 0f ? Duration : fallback);
        public Ease ResolveEase(Ease fallback) => OverrideEase ? Easing : fallback;
    }
}
