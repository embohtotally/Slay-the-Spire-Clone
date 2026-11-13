using System;
using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Serialization;

namespace PlushieOdyssey.Tweens
{
    /// <summary>
    /// Coordinates visual tracks, stagger timing, input and visibility for a panel.
    /// Open and close can use different sides.
    /// Place the driver on an always-active object when Deactivate When Closed is enabled.
    /// </summary>
    public class PanelTransitionTween : BaseTween
    {
        // Preserve the existing field paths so prefabs and overrides need no migration.
        [Header("Slide (anchored coordinates)")]
        [SerializeField] private Vector2 _openPosition = Vector2.zero;
        [SerializeField] private Vector2 _openStartOffset = new(2000f, 0f);
        [SerializeField] private Vector2 _closedOffset = new(-2000f, 0f);
        [SerializeField] private TransitionTrackTiming _slideTiming = new();
        [Tooltip("Additional slide tracks in stagger order, after the primary Target. Null targets are skipped.")]
        [SerializeField] private SlideTransitionTrack[] _additionalParts = Array.Empty<SlideTransitionTrack>();

        [Header("Stagger")]
        [Tooltip("Seconds between slide starts. Zero keeps all slides synchronized. Fade is scheduled separately.")]
        [SerializeField, Min(0f)] private float _staggerInterval = 0f;
        [Tooltip("Close slides in the opposite order. Each track's own Delay still applies.")]
        [SerializeField] private bool _reverseCloseOrder = true;

        [Header("Optional Fade")]
        [Tooltip("Opacity target, such as DimBackground. It does not have to be the moving Target.")]
        [FormerlySerializedAs("_canvasGroup")]
        [SerializeField] private CanvasGroup _fadeGroup;
        [SerializeField] private bool _fadeCanvasGroup = true;
        [SerializeField, Range(0f, 1f)] private float _openAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float _closedAlpha = 0f;
        [SerializeField] private TransitionTrackTiming _fadeTiming = new();

        [Header("Input")]
        [Tooltip("Use a CanvasGroup on the common panel root to gate content and background together. Its alpha is never changed. If unassigned, falls back to Fade Group for compatibility.")]
        [SerializeField] private CanvasGroup _inputGroup = null;
        [SerializeField] private bool _manageInput = true;

        [Header("Visibility")]
        [Tooltip("Positions the panel offscreen on initialization. Enable Play On Enable for an automatic entrance.")]
        [SerializeField] private bool _startClosed = false;
        [Tooltip("Deactivates the primary Target after closing. Keep this driver outside that hierarchy.")]
        [SerializeField] private bool _deactivateWhenClosed = false;

        private bool _opening = true;
        private bool _closed;
        private bool _preparing;
        private bool _enterFromOffset;

        private CanvasGroup InputGroup => _inputGroup != null ? _inputGroup : _fadeGroup;

        protected override bool SupportsRelativeAndSpeedBased => false;
        protected override bool SupportsLoops => false;
        protected override Ease TimelineEase => Ease.Linear;

        protected override void ResetTweenSettings()
        {
            base.ResetTweenSettings();
            ConfigureBaseSettings(Ease.InOutQuad, independentUpdate: true);
        }

#if UNITY_EDITOR
        [Button("Capture Current Position", enabledMode: EButtonEnableMode.Editor)]
        [ContextMenu("Capture Current Position")]
        private void CaptureCurrentPosition()
        {
            if (Application.isPlaying) return;
            if (Target == null)
            {
                Debug.LogWarning("[PanelTransitionTween] Assign a Target before capturing positions.", this);
                return;
            }

            UnityEditor.Undo.RecordObject(this, "Capture Current Position");
            _openPosition = Target.anchoredPosition;
            foreach (SlideTransitionTrack part in _additionalParts ?? Array.Empty<SlideTransitionTrack>())
            {
                if (part != null && part.Target != null) part.OpenPosition = part.Target.anchoredPosition;
            }
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        protected override bool InitializeTween()
        {
            if (_fadeGroup == null) _fadeGroup = Target.GetComponent<CanvasGroup>();
            _closed = _startClosed || !Target.gameObject.activeSelf;
            if (_startClosed) ApplyInstantState(false);
            else if (_closed) SetInputState(false);
            return true;
        }

        public void Open() => Play(false);
        public void Close() => PlayReverse(false);
        public void OpenInstant() => SetVisibilityInstant(true);
        public void CloseInstant() => SetVisibilityInstant(false);
        public override void Stop() => CloseInstant();

        public override void Complete()
        {
            if (Prepare(true, false)) base.Complete();
        }

        public override void CompleteReverse()
        {
            if (Prepare(false, false)) base.CompleteReverse();
        }

        public override void Play(bool resetAndPlay)
        {
            if (!Prepare(true, resetAndPlay)) return;
            base.Play(true);
        }

        public override void PlayReverse(bool resetAndPlay)
        {
            if (!Prepare(false, resetAndPlay)) return;
            base.PlayReverse(true);
        }

        private bool Prepare(bool opening, bool reset)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || _preparing || !EnsureInitialized()) return false;
            _preparing = true;
            try
            {
                _enterFromOffset = opening && (reset || _closed || !Target.gameObject.activeSelf);
                _opening = opening;
                if (opening) Target.gameObject.SetActive(true);
                DiscardTween();
                return true;
            }
            finally { _preparing = false; }
        }

        protected override Tween CreateTween()
        {
            var slides = new List<SlideTransitionTrack> { PrimarySlide() };
            var targets = new HashSet<RectTransform> { Target };
            foreach (SlideTransitionTrack track in _additionalParts ?? Array.Empty<SlideTransitionTrack>())
            {
                if (track == null || track.Target == null) continue;
                if (!targets.Add(track.Target))
                {
                    Debug.LogWarning("[PanelTransitionTween] Duplicate slide target ignored; use one track per RectTransform.", this);
                    continue;
                }
                slides.Add(track);
            }

            var scheduled = new List<(Tween Tween, float Start)>();
            float totalDuration = 0f;
            for (int i = 0; i < slides.Count; i++)
            {
                SlideTransitionTrack track = slides[i];
                int order = !_opening && _reverseCloseOrder ? slides.Count - 1 - i : i;
                float start = (track.Timing?.StartDelay ?? 0f) + order * Mathf.Max(0f, _staggerInterval);
                Schedule(track.CreateTween(_opening, _enterFromOffset, Duration, Easing), start);
            }

            if (_fadeCanvasGroup && _fadeGroup != null)
            {
                FadeTransitionTrack fade = FadeTrack();
                Schedule(fade.CreateTween(_opening, _enterFromOffset, Duration, Easing),
                    fade.Timing?.StartDelay ?? 0f);
            }

            Sequence sequence = DOTween.Sequence();
            foreach (var track in scheduled)
            {
                // Mirror placement, not the requested start delay, when the exit plays backwards.
                float position = _opening ? track.Start : totalDuration - track.Start - track.Tween.Duration();
                sequence.Insert(Mathf.Max(0f, position), track.Tween);
            }
            // A delayed exit needs its initial waiting interval at the END of this reversed timeline.
            if (sequence.Duration() < totalDuration)
                sequence.AppendInterval(totalDuration - sequence.Duration());
            return sequence.SetEase(Ease.Linear);

            void Schedule(Tween tween, float start)
            {
                if (tween == null) return;
                scheduled.Add((tween, start));
                totalDuration = Mathf.Max(totalDuration, start + tween.Duration());
            }
        }

        private SlideTransitionTrack PrimarySlide() => new()
        {
            Target = Target,
            OpenPosition = _openPosition,
            OpenStartOffset = _openStartOffset,
            ClosedOffset = _closedOffset,
            Timing = _slideTiming
        };

        private FadeTransitionTrack FadeTrack() => new()
        {
            Target = _fadeGroup,
            OpenAlpha = _openAlpha,
            ClosedAlpha = _closedAlpha,
            Timing = _fadeTiming
        };

        protected override void OnPlaybackStarted(bool reverse)
        {
            _closed = false;
            CanvasGroup inputGroup = InputGroup;
            if (!_manageInput || inputGroup == null) return;
            inputGroup.interactable = false;
            inputGroup.blocksRaycasts = true;
        }

        protected override void OnPlaybackCompleted(bool reverse)
        {
            _closed = reverse;
            SetInputState(!reverse);
            if (reverse && _deactivateWhenClosed) Target.gameObject.SetActive(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // A disabled owner cannot finish its sequence or release its input guard later.
            if (Application.isPlaying && Target != null && !_preparing) CloseInstant();
        }

        protected override void OnPoseSnapped(bool atEnd) => SetVisibilityInstant(atEnd);

        private void SetVisibilityInstant(bool open)
        {
            if (!Application.isPlaying || _preparing || !EnsureInitialized()) return;
            _preparing = true;
            try
            {
                DiscardTween();
                if (open) Target.gameObject.SetActive(true);
                ApplyInstantState(open);
                if (!open && _deactivateWhenClosed) Target.gameObject.SetActive(false);
            }
            finally { _preparing = false; }
        }

        private void ApplyInstantState(bool open)
        {
            _closed = !open;
            PrimarySlide().ApplyInstant(open);
            var targets = new HashSet<RectTransform> { Target };
            foreach (SlideTransitionTrack part in _additionalParts ?? Array.Empty<SlideTransitionTrack>())
            {
                if (part != null && part.Target != null && targets.Add(part.Target)) part.ApplyInstant(open);
            }
            if (_fadeCanvasGroup) FadeTrack().ApplyInstant(open);
            SetInputState(open);
        }

        private void SetInputState(bool open)
        {
            CanvasGroup inputGroup = InputGroup;
            if (!_manageInput || inputGroup == null) return;
            inputGroup.interactable = open;
            inputGroup.blocksRaycasts = open;
        }
    }
}
