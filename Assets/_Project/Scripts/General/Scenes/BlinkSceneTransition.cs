using System;
using System.Collections;
using PlushieOdyssey.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlushieOdyssey.Core.Scenes
{
    public enum BlinkTransitionPhase
    {
        Idle,
        Blink1Closing,
        Blink1Opening,
        Blink2Closing,
        LoadingScene,
        OpeningNewScene
    }

    /// <summary>
    /// Persistent procedural double-blink eyelid scene transition replacing the old Loading screen.
    /// Choreography:
    ///   1. Quick natural Blink 1 (curved eyelids snap shut, then flutter drowsily back open ~72%).
    ///   2. Deeper Blink 2 (curved eyelids sweep fully shut into dark velvet blackout).
    ///   3. Async scene swap while eyes are completely closed.
    ///   4. Smooth wake-up reveal: fires SceneRevealing as eyelids begin opening so destination scenes
    ///      choreograph their staggered procedural entrances live in front of the player.
    /// </summary>
    [DisallowMultipleComponent]
    public class BlinkSceneTransition : MonoBehaviour
    {
        private static BlinkSceneTransition s_instance;

        [Header("Double-Blink Timing (Unscaled Seconds)")]
        [SerializeField] private float blink1CloseDuration = 0.11f;
        [SerializeField] private float blink1ClosedHold = 0.03f;
        [SerializeField] private float blink1ReopenDuration = 0.15f;
        [SerializeField, Range(0.45f, 0.92f)] private float blink1ReopenAmount = 0.72f;
        [SerializeField] private float betweenBlinksPause = 0.08f;
        [SerializeField] private float blink2CloseDuration = 0.17f;
        [SerializeField] private float closedSceneBreath = 0.08f;
        [SerializeField] private float wakeOpenDuration = 0.36f;

        [Header("Visual Style")]
        [SerializeField] private Color eyelidColor = new(0.035f, 0.025f, 0.06f, 1f);
        [SerializeField] private Color drowsyDimColor = new(0.03f, 0.02f, 0.055f, 1f);
        [SerializeField] private float maxDrowsyDimAlpha = 0.28f;

        private Canvas _canvas;
        private CanvasGroup _blockerGroup;
        private Image _blackoutVeil;
        private RectTransform _topLidRt;
        private RectTransform _bottomLidRt;
        private Image _topLidImg;
        private Image _bottomLidImg;

        private static Sprite s_solidSprite;
        private static Sprite s_topLidSprite;
        private static Sprite s_bottomLidSprite;

        private Coroutine _activeRoutine;
        private string _pendingSceneName;
        private bool _isTransitioning;
        private bool _isEyesClosed;
        private float _openAmount = 1f;
        private BlinkTransitionPhase _phase = BlinkTransitionPhase.Idle;

        public static BlinkSceneTransition Instance => s_instance;
        public static bool IsTransitioning => s_instance != null && s_instance._isTransitioning;
        public static bool IsEyesClosed => s_instance != null && s_instance._isEyesClosed;
        public static float CurrentOpenAmount => s_instance != null ? s_instance._openAmount : 1f;
        public static BlinkTransitionPhase CurrentPhase => s_instance != null ? s_instance._phase : BlinkTransitionPhase.Idle;

        /// <summary>
        /// Fired on the exact frame the eyelids begin parting open on the newly loaded scene.
        /// Destination scenes hook this (or call RunWhenRevealing) to start staggered entrances.
        /// </summary>
        public static event Action<string> SceneRevealing;

        /// <summary>
        /// Fired once the eyelids finish opening completely on the newly loaded scene.
        /// </summary>
        public static event Action<string> SceneRevealed;

        public static BlinkSceneTransition EnsureInstance()
        {
            if (s_instance != null) return s_instance;

            s_instance = FindFirstObjectByType<BlinkSceneTransition>();
            if (s_instance != null) return s_instance;

            GameObject go = new("BlinkSceneTransition");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<BlinkSceneTransition>();
            return s_instance;
        }

        /// <summary>
        /// Runs <paramref name="onReveal"/> right as the eyelids begin opening after a blink transition,
        /// or immediately (next frame if requested) when launching a scene directly without a transition.
        /// </summary>
        public static void RunWhenRevealing(MonoBehaviour host, Action onReveal)
        {
            if (onReveal == null) return;

            if (!IsEyesClosed)
            {
                onReveal();
                return;
            }

            if (host == null || !host.isActiveAndEnabled)
            {
                void OneShot(string _)
                {
                    SceneRevealing -= OneShot;
                    onReveal();
                }
                SceneRevealing += OneShot;
                return;
            }

            host.StartCoroutine(WaitForRevealRoutine(onReveal));
        }

        private static IEnumerator WaitForRevealRoutine(Action onReveal)
        {
            bool fired = false;
            void HandleReveal(string _) => fired = true;
            SceneRevealing += HandleReveal;

            try
            {
                while (!fired && IsEyesClosed)
                {
                    yield return null;
                }
            }
            finally
            {
                SceneRevealing -= HandleReveal;
            }

            onReveal?.Invoke();
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlayIfNeeded();
            ApplyOpenAmount(1f, blackoutAlpha: 0f);
            SetOverlayVisible(false, blockInput: false);
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        public bool TransitionToScene(string sceneName, Action onBeforeSwap = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return false;
            if (_isTransitioning) return false;

            BuildOverlayIfNeeded();
            _pendingSceneName = sceneName;
            _activeRoutine = StartCoroutine(DoubleBlinkRoutine(sceneName, onBeforeSwap));
            return true;
        }

        private IEnumerator DoubleBlinkRoutine(string targetSceneName, Action onBeforeSwap)
        {
            _isTransitioning = true;
            _isEyesClosed = false;
            _phase = BlinkTransitionPhase.Blink1Closing;

            SetOverlayVisible(true, blockInput: true);
            ApplyOpenAmount(1f, blackoutAlpha: 0f);

            // -----------------------------------------------------------------
            // BLINK 1: Swift natural anticipation blink (close -> drowsy reopen)
            // -----------------------------------------------------------------
            yield return AnimateOpen(1f, 0f, blink1CloseDuration, EaseInQuad, 0f, maxDrowsyDimAlpha + 0.16f);

            if (blink1ClosedHold > 0f)
            {
                yield return WaitUnscaled(blink1ClosedHold);
            }

            _phase = BlinkTransitionPhase.Blink1Opening;
            yield return AnimateOpen(0f, blink1ReopenAmount, blink1ReopenDuration, EaseOutCubic, maxDrowsyDimAlpha + 0.16f, maxDrowsyDimAlpha);

            if (betweenBlinksPause > 0f)
            {
                yield return WaitUnscaled(betweenBlinksPause);
            }

            // -----------------------------------------------------------------
            // BLINK 2: Deep closing blink -> full blackout -> SWAP SCENE!
            // -----------------------------------------------------------------
            _phase = BlinkTransitionPhase.Blink2Closing;
            yield return AnimateOpen(blink1ReopenAmount, 0f, blink2CloseDuration, EaseInCubic, maxDrowsyDimAlpha, 1f);

            _isEyesClosed = true;
            _phase = BlinkTransitionPhase.LoadingScene;
            ApplyOpenAmount(0f, blackoutAlpha: 1f);

            // Restore time scale cleanly in case scene was paused or hit-stopped
            if (Time.timeScale <= 0.01f)
            {
                Time.timeScale = 1f;
            }

            onBeforeSwap?.Invoke();

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
            if (asyncLoad != null)
            {
                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
            }

            // Give the new scene 1 frame so Awake/Start prepare hidden pre-entrance states behind closed lids
            yield return null;

            if (closedSceneBreath > 0f)
            {
                yield return WaitUnscaled(closedSceneBreath);
            }

            // -----------------------------------------------------------------
            // WAKE: Eyelids smoothly part open -> destination scene staggers in!
            // -----------------------------------------------------------------
            _phase = BlinkTransitionPhase.OpeningNewScene;
            _isEyesClosed = false;

            try
            {
                SceneRevealing?.Invoke(targetSceneName);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            float elapsed = 0f;
            float dur = Mathf.Max(0.05f, wakeOpenDuration);
            bool inputReleased = false;

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float eased = EaseOutCubic(t);

                // Blackout veil clears slightly ahead of the parting curved lids for a vivid eyelid silhouette
                float veilAlpha = Mathf.Clamp01(1f - Mathf.Pow(t, 0.72f) * 1.12f);
                ApplyOpenAmount(eased, veilAlpha);

                if (!inputReleased && eased >= 0.58f && _blockerGroup != null)
                {
                    _blockerGroup.blocksRaycasts = false;
                    inputReleased = true;
                }

                yield return null;
            }

            ApplyOpenAmount(1f, blackoutAlpha: 0f);
            SetOverlayVisible(false, blockInput: false);

            _isTransitioning = false;
            _isEyesClosed = false;
            _phase = BlinkTransitionPhase.Idle;
            _activeRoutine = null;
            _pendingSceneName = null;

            try
            {
                SceneRevealed?.Invoke(targetSceneName);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private IEnumerator AnimateOpen(
            float fromOpen,
            float toOpen,
            float duration,
            Func<float, float> easeFn,
            float fromVeilAlpha,
            float toVeilAlpha)
        {
            float safeDur = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < safeDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDur);
                float eased = easeFn(t);
                float open = Mathf.Lerp(fromOpen, toOpen, eased);
                float veil = Mathf.Lerp(fromVeilAlpha, toVeilAlpha, eased);
                ApplyOpenAmount(open, veil);
                yield return null;
            }

            ApplyOpenAmount(toOpen, toVeilAlpha);
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void ApplyOpenAmount(float openAmount, float blackoutAlpha)
        {
            _openAmount = Mathf.Clamp01(openAmount);

            if (_canvas == null || _topLidRt == null || _bottomLidRt == null) return;

            RectTransform canvasRt = (RectTransform)_canvas.transform;
            float h = canvasRt.rect.height;
            if (h < 100f) h = 1080f;

            // Each curved eyelid covers 62% of screen height so the arched curves overlap completely at open=0
            // and retract fully beyond the top/bottom viewport edges at open=1.
            float lidHeight = h * 0.64f;
            float closedOverlap = h * 0.08f;

            _topLidRt.sizeDelta = new Vector2(0f, lidHeight);
            _bottomLidRt.sizeDelta = new Vector2(0f, lidHeight);

            float topClosedY = -lidHeight + closedOverlap;
            float topOpenY = lidHeight * 0.14f;
            _topLidRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(topClosedY, topOpenY, _openAmount));

            float botClosedY = lidHeight - closedOverlap;
            float botOpenY = -lidHeight * 0.14f;
            _bottomLidRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(botClosedY, botOpenY, _openAmount));

            if (_blackoutVeil != null)
            {
                Color c = drowsyDimColor;
                c.a = Mathf.Clamp01(blackoutAlpha);
                _blackoutVeil.color = c;
            }
        }

        private void SetOverlayVisible(bool visible, bool blockInput)
        {
            if (_canvas != null) _canvas.enabled = visible;
            if (_blockerGroup != null)
            {
                _blockerGroup.alpha = visible ? 1f : 0f;
                _blockerGroup.blocksRaycasts = visible && blockInput;
                _blockerGroup.interactable = false;
            }
        }

        private void BuildOverlayIfNeeded()
        {
            if (_canvas != null) return;

            GameObject canvasGo = new("BlinkOverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32760;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _blockerGroup = canvasGo.GetComponent<CanvasGroup>();
            _blockerGroup.interactable = false;
            _blockerGroup.blocksRaycasts = false;

            // Full-screen drowsy dim / full blackout veil (behind the curved eyelids)
            GameObject veilGo = new("BlackoutVeil", typeof(RectTransform), typeof(Image));
            RectTransform veilRt = (RectTransform)veilGo.transform;
            veilRt.SetParent(canvasGo.transform, false);
            veilRt.anchorMin = Vector2.zero;
            veilRt.anchorMax = Vector2.one;
            veilRt.offsetMin = Vector2.zero;
            veilRt.offsetMax = Vector2.zero;
            _blackoutVeil = veilGo.GetComponent<Image>();
            _blackoutVeil.sprite = GetSolidSprite();
            _blackoutVeil.color = new Color(drowsyDimColor.r, drowsyDimColor.g, drowsyDimColor.b, 0f);
            _blackoutVeil.raycastTarget = true;

            // Upper curved organic eyelid
            GameObject topGo = new("UpperEyelid", typeof(RectTransform), typeof(Image));
            _topLidRt = (RectTransform)topGo.transform;
            _topLidRt.SetParent(canvasGo.transform, false);
            _topLidRt.anchorMin = new Vector2(0f, 1f);
            _topLidRt.anchorMax = new Vector2(1f, 1f);
            _topLidRt.pivot = new Vector2(0.5f, 0f);
            _topLidImg = topGo.GetComponent<Image>();
            _topLidImg.sprite = GetTopEyelidSprite();
            _topLidImg.color = eyelidColor;
            _topLidImg.type = Image.Type.Simple;
            _topLidImg.raycastTarget = false;

            // Lower curved organic eyelid
            GameObject botGo = new("LowerEyelid", typeof(RectTransform), typeof(Image));
            _bottomLidRt = (RectTransform)botGo.transform;
            _bottomLidRt.SetParent(canvasGo.transform, false);
            _bottomLidRt.anchorMin = new Vector2(0f, 0f);
            _bottomLidRt.anchorMax = new Vector2(1f, 0f);
            _bottomLidRt.pivot = new Vector2(0.5f, 1f);
            _bottomLidImg = botGo.GetComponent<Image>();
            _bottomLidImg.sprite = GetBottomEyelidSprite();
            _bottomLidImg.color = eyelidColor;
            _bottomLidImg.type = Image.Type.Simple;
            _bottomLidImg.raycastTarget = false;
        }

        private static Sprite GetSolidSprite()
        {
            if (s_solidSprite != null) return s_solidSprite;
            Texture2D tex = Texture2D.whiteTexture;
            s_solidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return s_solidSprite;
        }

        private static Sprite GetTopEyelidSprite()
        {
            if (s_topLidSprite != null) return s_topLidSprite;
            s_topLidSprite = BuildCurvedEyelidSprite(isTopLid: true);
            return s_topLidSprite;
        }

        private static Sprite GetBottomEyelidSprite()
        {
            if (s_bottomLidSprite != null) return s_bottomLidSprite;
            s_bottomLidSprite = BuildCurvedEyelidSprite(isTopLid: false);
            return s_bottomLidSprite;
        }

        /// <summary>
        /// Procedurally generates an arched, softly feathered eyelid texture so the blink
        /// looks unmistakably like organic closing eyelids rather than flat black rectangles.
        /// </summary>
        private static Sprite BuildCurvedEyelidSprite(bool isTopLid)
        {
            const int width = 512;
            const int height = 256;
            Texture2D tex = new(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[width * height];
            const float feather = 0.085f; // Soft eyelash-shadow edge gradient

            for (int y = 0; y < height; y++)
            {
                // Normalized distance from the inner opening edge (0 = opening edge, 1 = outer screen edge)
                float v = isTopLid ? (float)y / (height - 1) : 1f - (float)y / (height - 1);

                for (int x = 0; x < width; x++)
                {
                    // Horizontal coordinate [-1, 1] from center of eye to left/right corners
                    float nx = ((float)x / (width - 1)) * 2f - 1f;

                    // Parabolic/elliptical eye arch: higher opening in the center, curving shut at the corners
                    float arch = 0.24f * (1f - nx * nx);
                    float edge = v - arch;

                    float alpha;
                    if (edge <= -feather)
                    {
                        alpha = 0f;
                    }
                    else if (edge >= feather)
                    {
                        alpha = 1f;
                    }
                    else
                    {
                        float t = (edge + feather) / (2f * feather);
                        // Smoothstep feather with slightly darker lash-shadow falloff
                        alpha = t * t * (3f - 2f * t);
                    }

                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float EaseInQuad(float t) => t * t;
        private static float EaseInCubic(float t) => t * t * t;
        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }
    }
}

