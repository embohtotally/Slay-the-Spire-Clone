using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

namespace PlushieOdyssey.Tweens
{
    public abstract class BaseTween : MonoBehaviour
    {
        [SerializeField] private RectTransform _target;
        [SerializeField, Min(0.001f)] private float _duration = 1f;
        [SerializeField] private bool _playOnEnable = false;

        [Tooltip("Inspector visibility only. The saved settings below always apply.")]
        [BoxGroup("Base Settings"), SerializeField] private bool _changeBaseSettings = false;

        // Reading the toggles through properties keeps them Inspector-only without unused-field warnings.
        private bool ShowBaseSettings => _changeBaseSettings;
        private bool ShowLifetimeSettings => _changeLifetimeSettings;

        #region Base Settings
        [ShowIf(nameof(ShowBaseSettings))]
        [BoxGroup("Base Settings"), SerializeField]
        private Ease _easing = Ease.OutQuad;

        [ShowIf(nameof(ShowBaseSettings))]
        [BoxGroup("Base Settings"), SerializeField]
        private float _delay = 0f;

        [ShowIf(nameof(ShowBaseSettings))]
        [BoxGroup("Base Settings"), SerializeField, MinValue(-1)]
        private int _loopCount = 1;

        [ShowIf(nameof(ShowBaseSettings))]
        [BoxGroup("Base Settings"), SerializeField]
        private LoopType _loopType = LoopType.Restart;

        [ShowIf(nameof(ShowBaseSettings))]
        [BoxGroup("Base Settings"), SerializeField]
        private UpdateType _updateType = UpdateType.Normal;

        [ShowIf(nameof(ShowBaseSettings))]
        [BoxGroup("Base Settings"), SerializeField]
        private bool _isIndependentUpdate = false;

        [ShowIf(nameof(ShowBaseSettings))]
        [BoxGroup("Base Settings"), SerializeField]
        private bool _isRelative = false;

        [ShowIf(nameof(ShowBaseSettings))]
        [BoxGroup("Base Settings"), SerializeField]
        private bool _isSpeedBased = false;
        #endregion

        [Tooltip("Inspector visibility only. Set An Id controls whether the saved ID is applied.")]
        [BoxGroup("Lifetime Settings"), SerializeField] private bool _changeLifetimeSettings = false;

        #region Lifetime Settings
        // [ShowIf(nameof(ShowLifetimeSettings))]
        // [BoxGroup("Lifetime Settings"), SerializeField]
        // private bool _isAutoKill = true;

        // [ShowIf(nameof(ShowLifetimeSettings))]
        // [BoxGroup("Lifetime Settings"), SerializeField]
        // private bool _isRecyclable = false;

        [ShowIf(nameof(ShowLifetimeSettings))]
        [BoxGroup("Lifetime Settings"), SerializeField]
        private bool _setAnId = false;

        [ShowIf(EConditionOperator.And, new string[] { nameof(ShowLifetimeSettings), nameof(_setAnId) })]
        [BoxGroup("Lifetime Settings"), SerializeField]
        private string _setId = string.Empty;
        #endregion

        [BoxGroup("Events"), SerializeField] private UnityEvent _onPlay = new();
        [BoxGroup("Events"), SerializeField] private UnityEvent _onReversePlay = new();
        [BoxGroup("Events"), SerializeField] private UnityEvent _onComplete = new();
        [BoxGroup("Events"), SerializeField] private UnityEvent _onReverseComplete = new();

        private readonly TweenParams _tweenParams = new();

        private Canvas _canvas;
        private RectTransform _canvasTransform;
        private Tween _tween;
        private bool _initialized;
        private bool _seeking;
        private bool _playbackRequested;
        private bool _playingReverse;
        private int _playbackVersion;

        protected RectTransform Target => _target;
        protected float Duration => Mathf.Max(0.001f, _duration);
        protected Ease Easing => _easing;
        // Sequences with individually eased tracks must keep their outer clock linear.
        protected virtual Ease TimelineEase => Easing;
        protected TweenParams TweenParams => _tweenParams;
        protected Canvas Canvas => _canvas;
        protected RectTransform CanvasTransform => _canvasTransform;
        protected Tween Tween => _tween;

        protected virtual bool SupportsRelativeAndSpeedBased => true;
        protected virtual bool SupportsLoops => true;
        protected bool IsRelative => SupportsRelativeAndSpeedBased && _isRelative;

        public bool IsPlaying => _tween != null && _tween.IsActive() && _tween.IsPlaying();

        protected virtual void Reset()
        {
            if (_target == null) _target = transform as RectTransform;
            ResetTweenSettings();
        }

        // Unity calls Reset when adding a component. Defaults are written into the same fields
        // the Inspector edits, never substituted for them at runtime.
        protected virtual void ResetTweenSettings()
        {
            ConfigureBaseSettings(Ease.OutQuad);
            _delay = 0f;
            _updateType = UpdateType.Normal;
            _isRelative = false;
            _isSpeedBased = false;
        }

        protected void ConfigureBaseSettings(Ease easing, int loopCount = 1,
            LoopType loopType = LoopType.Restart, bool independentUpdate = false)
        {
            _easing = easing;
            _loopCount = loopCount;
            _loopType = loopType;
            _isIndependentUpdate = independentUpdate;
        }

        [ContextMenu("Apply Default Tween Settings")]
        private void ApplyDefaultTweenSettings()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Apply default tween settings before entering Play Mode.", this);
                return;
            }

            // Unlike resetting the entire component, keep its targets, duration and events.
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Apply Default Tween Settings");
#endif
            ResetTweenSettings();
#if UNITY_EDITOR
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        protected virtual void Awake()
        {
            EnsureInitialized();
        }

        protected bool EnsureInitialized()
        {
            if (_initialized) return true;

            if (_target == null) _target = transform as RectTransform;
            if (_target == null)
            {
                Debug.LogError($"[{GetType().Name}] Assign a Target RectTransform.", this);
                return false;
            }

            _canvas = _target.GetComponentInParent<Canvas>(true);
            _canvasTransform = _canvas != null ? _canvas.transform as RectTransform : null;

            _tweenParams
                .SetEase(_easing)
                .SetDelay(_delay)
                .SetLoops(SupportsLoops ? (_loopCount == 0 ? 1 : _loopCount) : 1, _loopType)
                .SetUpdate(_updateType, _isIndependentUpdate)
                .SetRelative(SupportsRelativeAndSpeedBased && _isRelative)
                .SetSpeedBased(SupportsRelativeAndSpeedBased && _isSpeedBased);

            if ((!SupportsRelativeAndSpeedBased && (_isRelative || _isSpeedBased)) ||
                (!SupportsLoops && _loopCount != 0 && _loopCount != 1))
            {
                Debug.LogWarning($"[{GetType().Name}] Relative/speed-based or looping settings are not supported by this transition and will be ignored.", this);
            }

            if (_setAnId) _tweenParams.SetId(_setId);
            _initialized = InitializeTween();
            return _initialized;
        }

        // Cache additional targets here. A Canvas is optional unless a subclass needs its size.
        protected virtual bool InitializeTween() => true;

        protected virtual void OnEnable()
        {
            if (_playOnEnable) Play(true);
        }
        protected virtual void OnDisable()
        {
            Pause();
        }

        protected virtual void OnDestroy()
        {
            DiscardTween();
        }

        protected abstract Tween CreateTween();

        protected bool EnsureTween()
        {
            if (!EnsureInitialized()) return false;
            if (_tween != null && _tween.IsActive()) return true;

            _tween = CreateTween();
            if (_tween == null) return false;

            _tween.SetAs(_tweenParams)
                .SetEase(TimelineEase)
                .SetAutoKill(false)
                .SetRecyclable(false)
                .Pause()
                .OnComplete(() => FinishPlayback(false))
                .OnRewind(() => FinishPlayback(true));
            _tween.ForceInit();
            return true;
        }

        protected void DiscardTween()
        {
            _playbackVersion++;
            _playbackRequested = false;
            _tween?.Kill();
            _tween = null;
        }

        public void Play() => Play(false);
        public void PlayReverse() => PlayReverse(false);

        public virtual void Play(bool resetAndPlay)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !EnsureTween()) return;

            if (resetAndPlay || _tween.IsComplete()) Seek(0f);
            BeginPlayback(false);
        }

        public virtual void PlayReverse(bool resetAndPlay)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !EnsureTween()) return;

            if (resetAndPlay || _tween.Elapsed() <= 0f) Seek(EndTime);
            BeginPlayback(true);
        }

        [Button(enabledMode: EButtonEnableMode.Playmode)]
        public void Restart() => Play(true);

        [Button(enabledMode: EButtonEnableMode.Playmode)]
        public void RestartReverse() => PlayReverse(true);

        public void Pause()
        {
            _playbackVersion++;
            _playbackRequested = false;
            _tween?.Pause();
        }

        public virtual void Stop() => SetToStart();

        // Snapping does not fire start/completion events. Complete methods do fire completion.
        public void SetToStart()
        {
            if (!Application.isPlaying || !EnsureTween()) return;
            Pause();
            Seek(0f);
            OnPoseSnapped(false);
        }

        public void SetToEnd()
        {
            if (!Application.isPlaying || !EnsureTween()) return;
            Pause();
            Seek(EndTime);
            OnPoseSnapped(true);
        }

        public virtual void Complete()
        {
            if (!Application.isPlaying || !EnsureTween()) return;
            Pause();
            Seek(EndTime);
            OnPlaybackCompleted(false);
            _onComplete.Invoke();
        }

        public virtual void CompleteReverse()
        {
            if (!Application.isPlaying || !EnsureTween()) return;
            Pause();
            Seek(0f);
            OnPlaybackCompleted(true);
            _onReverseComplete.Invoke();
        }

        private float EndTime
        {
            get
            {
                float duration = _tween.Duration();
                return float.IsInfinity(duration) ? _tween.Duration(false) : duration;
            }
        }

        private void Seek(float time)
        {
            _seeking = true;
            try { _tween.Goto(time, false); }
            finally { _seeking = false; }
        }

        private void BeginPlayback(bool reverse)
        {
            Tween current = _tween;
            int version = ++_playbackVersion;
            _playingReverse = reverse;
            _playbackRequested = true;
            OnPlaybackStarted(reverse);
            if (reverse) _onReversePlay.Invoke();
            else _onPlay.Invoke();

            // A UnityEvent is allowed to close, disable, or replace the animation immediately.
            if (version != _playbackVersion || current != _tween || !isActiveAndEnabled) return;
            if (reverse) current.PlayBackwards();
            else current.PlayForward();
        }

        private void FinishPlayback(bool reverse)
        {
            if (_seeking || !_playbackRequested || reverse != _playingReverse) return;
            _playbackRequested = false;
            OnPlaybackCompleted(reverse);
            if (reverse) _onReverseComplete.Invoke();
            else _onComplete.Invoke();
        }

        protected virtual void OnPlaybackStarted(bool reverse) { }
        protected virtual void OnPlaybackCompleted(bool reverse) { }
        protected virtual void OnPoseSnapped(bool atEnd) { }
    }
}
