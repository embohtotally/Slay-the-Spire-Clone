using System;
using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.UI.Combat
{
    /// <summary>
    /// Procedural Slay-the-Spire style deck reshuffle animation rendered natively in Canvas UI space:
    /// - Spawns a high, sweeping staggered fan of spinning card-backs flying along curved Bézier arcs
    ///   from the Discard Pile (bottom-right) back into the Draw Pile (bottom-left)
    /// - Each card leaves a luminous dual-layer comet ribbon trail (UIProceduralRibbonTrail),
    ///   translucent motion-blur card afterimages, and sparkling golden star motes
    /// - Punches/recoils both pile icons, ticks live pile counters as cards depart & arrive,
    ///   and emits golden impact shockwave rings on the Draw Pile as the reshuffled deck lands.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeckShuffleAnimatorUI : MonoBehaviour
    {
        public static DeckShuffleAnimatorUI Instance { get; private set; }

        [Header("Optional Custom Art Override (Procedural Celestial Card Back Used When Empty)")]
        [SerializeField] private Sprite _cardBackSprite;

        [Header("Shuffle Choreography")]
        [SerializeField, Range(4, 20)] private int _minVisualCards = 6;
        [SerializeField, Range(6, 24)] private int _maxVisualCards = 14;
        [SerializeField, Min(0.01f)] private float _launchStagger = 0.028f;
        [SerializeField, Min(0.15f)] private float _cardFlightDuration = 0.44f;
        [SerializeField, Min(0.05f)] private float _trailLifetime = 0.24f;
        [SerializeField, Min(4f)] private float _trailWidth = 44f;

        [Header("Trail & Glow Palette")]
        [SerializeField] private Color _trailHeadColor = new(1f, 0.99f, 0.88f, 1f);
        [SerializeField] private Color _trailMidColor = new(1f, 0.72f, 0.16f, 0.92f);
        [SerializeField] private Color _trailTailColor = new(0.86f, 0.30f, 0.08f, 0f);
        [SerializeField] private Color _trailOuterGlowColor = new(0.28f, 0.12f, 0.44f, 0.55f);
        [SerializeField] private Color _sparkleColor = new(1f, 0.92f, 0.46f, 1f);
        [SerializeField] private Color _afterimageTint = new(1f, 0.84f, 0.42f, 0.48f);

        private sealed class FlyingShuffleCard
        {
            public RectTransform CardRt;
            public Image CardImage;
            public UIProceduralRibbonTrail Trail;
            public Vector2 StartPos;
            public Vector2 ControlPos;
            public Vector2 EndPos;
            public float LaunchDelay;
            public float Elapsed;
            public float Duration;
            public float SpinSpeed;
            public float FlipFrequency;
            public float StartAngle;
            public float BaseScale;
            public float SparkleTimer;
            public float AfterimageTimer;
            public bool Launched;
            public bool Landed;
            public float PostLandElapsed;
            public Action OnLaunch;
            public Action OnLand;
        }

        private sealed class CardAfterimage
        {
            public RectTransform Rt;
            public Image Img;
            public float Elapsed;
            public float Lifetime;
            public Color StartColor;
            public Vector3 StartScale;
        }

        private sealed class SparkleMote
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Velocity;
            public float Elapsed;
            public float Lifetime;
            public float StartScale;
            public Color BaseColor;
        }

        private sealed class ImpactRing
        {
            public RectTransform Rt;
            public Image Img;
            public float Elapsed;
            public float Duration;
            public float StartSize;
            public float EndSize;
            public Color BaseColor;
        }

        private static Sprite _proceduralCardBack;
        private static Sprite _proceduralSparkle;
        private static Sprite _proceduralRing;

        private Canvas _rootCanvas;
        private RectTransform _layerRoot;
        private RectTransform _trailsContainer;
        private RectTransform _cardsContainer;
        private readonly List<FlyingShuffleCard> _flyingCards = new();
        private readonly List<CardAfterimage> _afterimages = new();
        private readonly List<SparkleMote> _sparkles = new();
        private readonly List<ImpactRing> _rings = new();

        public bool IsAnimating => _flyingCards.Count > 0 || _afterimages.Count > 0 || _sparkles.Count > 0 || _rings.Count > 0;
        public int ActiveFlyingCardCount => _flyingCards.Count;
        public int ActiveSparkleCount => _sparkles.Count;
        public int ActiveAfterimageCount => _afterimages.Count;

        public int TotalActiveTrailSamples
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _flyingCards.Count; i++)
                {
                    if (_flyingCards[i].Trail != null) sum += _flyingCards[i].Trail.SampleCount;
                }
                return sum;
            }
        }

        public static DeckShuffleAnimatorUI GetOrCreate(Canvas rootCanvas)
        {
            if (Instance != null && Instance._rootCanvas == rootCanvas)
            {
                return Instance;
            }

            if (rootCanvas == null) rootCanvas = FindFirstObjectByType<Canvas>()?.rootCanvas;
            if (rootCanvas == null) return null;

            DeckShuffleAnimatorUI existing = rootCanvas.GetComponentInChildren<DeckShuffleAnimatorUI>(true);
            if (existing != null)
            {
                existing.Initialize(rootCanvas);
                return existing;
            }

            GameObject go = new("DeckShuffleVfxLayer", typeof(RectTransform), typeof(CanvasGroup), typeof(DeckShuffleAnimatorUI));
            go.layer = rootCanvas.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(rootCanvas.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            DeckShuffleAnimatorUI animator = go.GetComponent<DeckShuffleAnimatorUI>();
            animator.Initialize(rootCanvas);
            return animator;
        }

        private void Awake()
        {
            Instance = this;
            _layerRoot = transform as RectTransform;
        }

        private void OnDisable()
        {
            ClearAllVisuals();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Initialize(Canvas rootCanvas)
        {
            _rootCanvas = rootCanvas;
            _layerRoot = transform as RectTransform;
            EnsureSubLayers();
            Instance = this;
        }

        private void EnsureSubLayers()
        {
            if (_layerRoot == null) return;

            if (_trailsContainer == null)
            {
                GameObject trailsGo = new("TrailsLayer", typeof(RectTransform));
                trailsGo.layer = gameObject.layer;
                _trailsContainer = (RectTransform)trailsGo.transform;
                _trailsContainer.SetParent(_layerRoot, false);
                _trailsContainer.anchorMin = Vector2.zero;
                _trailsContainer.anchorMax = Vector2.one;
                _trailsContainer.offsetMin = Vector2.zero;
                _trailsContainer.offsetMax = Vector2.zero;
            }

            if (_cardsContainer == null)
            {
                GameObject cardsGo = new("CardsLayer", typeof(RectTransform));
                cardsGo.layer = gameObject.layer;
                _cardsContainer = (RectTransform)cardsGo.transform;
                _cardsContainer.SetParent(_layerRoot, false);
                _cardsContainer.anchorMin = Vector2.zero;
                _cardsContainer.anchorMax = Vector2.one;
                _cardsContainer.offsetMin = Vector2.zero;
                _cardsContainer.offsetMax = Vector2.zero;
            }

            _trailsContainer.SetSiblingIndex(0);
            _cardsContainer.SetSiblingIndex(1);
        }

        /// <summary>
        /// Plays the juicy procedural shuffle wave from Discard Pile -> Draw Pile and returns the recommended
        /// wait time (in seconds) before dealing newly drawn cards so the shuffle arc visibly slams into the deck first.
        /// </summary>
        public float PlayShuffle(
            Transform discardPileTransform,
            Transform drawPileTransform,
            int totalShuffledCards,
            Action<int> onDiscardTick = null,
            Action<int> onDrawTick = null,
            Action onAllLanded = null)
        {
            if (_rootCanvas == null) _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_layerRoot == null) _layerRoot = transform as RectTransform;
            if (_rootCanvas == null || _layerRoot == null || totalShuffledCards <= 0)
            {
                onAllLanded?.Invoke();
                return 0f;
            }

            EnsureSubLayers();
            _layerRoot.SetAsLastSibling();

            Vector2 startLocal = ResolveCanvasLocalPosition(discardPileTransform, new Vector2(820f, -440f));
            Vector2 endLocal = ResolveCanvasLocalPosition(drawPileTransform, new Vector2(-820f, -390f));

            int visualCount = Mathf.Clamp(totalShuffledCards, _minVisualCards, _maxVisualCards);
            Sprite cardSprite = _cardBackSprite != null ? _cardBackSprite : GetProceduralCardBackSprite();

            Vector2 mid = (startLocal + endLocal) * 0.5f;
            float spanX = Mathf.Abs(startLocal.x - endLocal.x);
            int landedCounter = 0;

            // Initial punchy launch ring on the Discard Pile
            SpawnImpactRing(startLocal, 44f, 185f, 0.24f, new Color(1f, 0.80f, 0.30f, 0.85f));

            for (int i = 0; i < visualCount; i++)
            {
                float norm = visualCount > 1 ? (float)i / (visualCount - 1) : 0.5f;

                // High, sweeping mid-screen Bézier arc (remember Quadratic Bézier apex is 0.5 * arcHeight above baseline!)
                float arcHeight = Mathf.Lerp(480f, 920f, Mathf.Sin(norm * Mathf.PI) * 0.65f + UnityEngine.Random.Range(0.06f, 0.36f));
                float horizontalShift = Mathf.Lerp(-spanX * 0.16f, spanX * 0.16f, norm) + UnityEngine.Random.Range(-85f, 85f);
                Vector2 control = new Vector2(mid.x + horizontalShift, Mathf.Max(startLocal.y, endLocal.y) + arcHeight);

                Vector2 startJitter = startLocal + UnityEngine.Random.insideUnitCircle * 18f;
                Vector2 endJitter = endLocal + UnityEngine.Random.insideUnitCircle * 12f;

                // Ribbon Trail Graphic (explicitly carries CanvasRenderer and lives on TrailsLayer behind cards)
                GameObject trailGo = new($"ShuffleTrail_{i:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(UIProceduralRibbonTrail));
                trailGo.layer = gameObject.layer;
                RectTransform trailRt = (RectTransform)trailGo.transform;
                trailRt.SetParent(_trailsContainer, false);
                trailRt.anchorMin = Vector2.zero;
                trailRt.anchorMax = Vector2.one;
                trailRt.offsetMin = Vector2.zero;
                trailRt.offsetMax = Vector2.zero;

                UIProceduralRibbonTrail trail = trailGo.GetComponent<UIProceduralRibbonTrail>();
                trail.Configure(_trailWidth, _trailLifetime, _trailHeadColor, _trailMidColor, _trailTailColor, _trailOuterGlowColor);

                // Flying Card-Back Phantom (lives on CardsLayer in front of trails)
                GameObject cardGo = new($"ShuffleCard_{i:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cardGo.layer = gameObject.layer;
                RectTransform cardRt = (RectTransform)cardGo.transform;
                cardRt.SetParent(_cardsContainer, false);
                cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.sizeDelta = new Vector2(96f, 134f);
                cardRt.anchoredPosition = startJitter;
                cardRt.localScale = Vector3.zero;

                Image cardImg = cardGo.GetComponent<Image>();
                cardImg.sprite = cardSprite;
                cardImg.color = Color.Lerp(Color.white, new Color(1f, 0.94f, 0.80f, 1f), UnityEngine.Random.value * 0.35f);
                cardImg.raycastTarget = false;

                int capturedIndex = i;
                bool isLast = i == visualCount - 1;
                float flightDur = _cardFlightDuration + UnityEngine.Random.Range(-0.04f, 0.05f);

                var flying = new FlyingShuffleCard
                {
                    CardRt = cardRt,
                    CardImage = cardImg,
                    Trail = trail,
                    StartPos = startJitter,
                    ControlPos = control,
                    EndPos = endJitter,
                    LaunchDelay = i * _launchStagger,
                    Elapsed = 0f,
                    Duration = flightDur,
                    StartAngle = UnityEngine.Random.Range(-22f, 22f),
                    SpinSpeed = UnityEngine.Random.Range(420f, 640f) * (i % 2 == 0 ? 1f : -1f),
                    FlipFrequency = UnityEngine.Random.Range(2.0f, 3.2f),
                    BaseScale = UnityEngine.Random.Range(0.94f, 1.12f),
                    SparkleTimer = UnityEngine.Random.Range(0.01f, 0.03f),
                    AfterimageTimer = 0.025f,
                    OnLaunch = () =>
                    {
                        int remainingDiscard = Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(totalShuffledCards - 1, 0, (float)capturedIndex / Mathf.Max(1, visualCount - 1))));
                        if (isLast) remainingDiscard = 0;
                        onDiscardTick?.Invoke(remainingDiscard);
                        PunchPileTransform(discardPileTransform, 0.86f, 0.10f, 6f);
                    },
                    OnLand = () =>
                    {
                        landedCounter++;
                        int arrivedDraw = isLast
                            ? totalShuffledCards
                            : Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1, totalShuffledCards, (float)landedCounter / visualCount)), 1, totalShuffledCards);
                        onDrawTick?.Invoke(arrivedDraw);

                        PunchPileTransform(drawPileTransform, isLast ? 1.30f : 1.16f, isLast ? 0.18f : 0.11f, isLast ? 10f : 5f);

                        if (capturedIndex % 2 == 0 || isLast)
                        {
                            float endSize = isLast ? 260f : 155f;
                            float ringDur = isLast ? 0.30f : 0.20f;
                            Color ringCol = isLast
                                ? new Color(1f, 0.90f, 0.42f, 0.96f)
                                : new Color(1f, 0.80f, 0.34f, 0.65f);
                            SpawnImpactRing(endLocal, 36f, endSize, ringDur, ringCol);
                        }

                        int burstCount = isLast ? 10 : 3;
                        for (int s = 0; s < burstCount; s++)
                        {
                            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(85f, isLast ? 260f : 165f);
                            SpawnSparkle(endLocal + UnityEngine.Random.insideUnitCircle * 14f, dir, UnityEngine.Random.Range(18f, isLast ? 34f : 24f), UnityEngine.Random.Range(0.18f, 0.30f));
                        }

                        if (isLast)
                        {
                            Tune.SFX(SfxID.DrawCard);
                            onAllLanded?.Invoke();
                        }
                    }
                };

                _flyingCards.Add(flying);
            }

            float totalStagger = (visualCount - 1) * _launchStagger;
            return Mathf.Clamp(totalStagger * 0.68f + _cardFlightDuration * 0.72f, 0.34f, 0.56f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            UpdateFlyingCards(dt);
            UpdateAfterimages(dt);
            UpdateSparkles(dt);
            UpdateImpactRings(dt);
        }

        private void UpdateFlyingCards(float dt)
        {
            for (int i = _flyingCards.Count - 1; i >= 0; i--)
            {
                FlyingShuffleCard card = _flyingCards[i];

                if (card.LaunchDelay > 0f)
                {
                    card.LaunchDelay -= dt;
                    continue;
                }

                if (!card.Launched)
                {
                    card.Launched = true;
                    card.OnLaunch?.Invoke();
                    card.Trail?.AddPoint(card.StartPos);
                }

                if (!card.Landed)
                {
                    card.Elapsed += dt;
                    float rawT = Mathf.Clamp01(card.Elapsed / Mathf.Max(0.01f, card.Duration));
                    float easedT = rawT < 0.5f
                        ? 2f * rawT * rawT
                        : 1f - Mathf.Pow(-2f * rawT + 2f, 2f) * 0.5f;

                    Vector2 pos = EvaluateQuadraticBezier(card.StartPos, card.ControlPos, card.EndPos, easedT);
                    if (card.CardRt != null)
                    {
                        card.CardRt.anchoredPosition = pos;

                        float envelope = rawT < 0.16f
                            ? Mathf.Lerp(0.42f, 1.10f, rawT / 0.16f)
                            : (rawT > 0.78f
                                ? Mathf.Lerp(1.0f, 0.44f, (rawT - 0.78f) / 0.22f)
                                : Mathf.Lerp(1.10f, 1.0f, (rawT - 0.16f) / 0.62f));

                        float flipCos = Mathf.Cos(rawT * Mathf.PI * card.FlipFrequency);
                        float scaleX = Mathf.Max(0.32f, Mathf.Abs(flipCos)) * envelope * card.BaseScale;
                        float scaleY = envelope * card.BaseScale;
                        card.CardRt.localScale = new Vector3(scaleX, scaleY, 1f);

                        float angle = card.StartAngle + card.SpinSpeed * card.Elapsed;
                        card.CardRt.localRotation = Quaternion.Euler(0f, 0f, angle);

                        // Drop translucent golden ghost card afterimages along the sweeping arc!
                        card.AfterimageTimer -= dt;
                        if (card.AfterimageTimer <= 0f && rawT > 0.08f && rawT < 0.90f)
                        {
                            card.AfterimageTimer = 0.048f;
                            SpawnAfterimage(card.CardImage.sprite, pos, card.CardRt.localRotation, card.CardRt.localScale, 0.16f);
                        }
                    }

                    if (card.Trail != null)
                    {
                        card.Trail.AddPoint(pos);
                        card.Trail.Tick(dt);
                    }

                    card.SparkleTimer -= dt;
                    if (card.SparkleTimer <= 0f)
                    {
                        card.SparkleTimer = UnityEngine.Random.Range(0.04f, 0.075f);
                        Vector2 drift = new Vector2(UnityEngine.Random.Range(-55f, 55f), UnityEngine.Random.Range(35f, 110f));
                        SpawnSparkle(pos + UnityEngine.Random.insideUnitCircle * 14f, drift, UnityEngine.Random.Range(18f, 32f), UnityEngine.Random.Range(0.20f, 0.34f));
                    }

                    if (rawT >= 1f)
                    {
                        card.Landed = true;
                        card.PostLandElapsed = 0f;
                        card.Trail?.AddPoint(card.EndPos);
                        if (card.CardRt != null) card.CardRt.gameObject.SetActive(false);
                        card.OnLand?.Invoke();
                    }
                }
                else
                {
                    card.PostLandElapsed += dt;
                    card.Trail?.Tick(dt);

                    if (card.Trail == null || !card.Trail.HasPoints || card.PostLandElapsed > _trailLifetime + 0.05f)
                    {
                        if (card.CardRt != null) Destroy(card.CardRt.gameObject);
                        if (card.Trail != null) Destroy(card.Trail.gameObject);
                        _flyingCards.RemoveAt(i);
                    }
                }
            }
        }

        private void UpdateAfterimages(float dt)
        {
            for (int i = _afterimages.Count - 1; i >= 0; i--)
            {
                CardAfterimage ghost = _afterimages[i];
                ghost.Elapsed += dt;
                float t = Mathf.Clamp01(ghost.Elapsed / Mathf.Max(0.01f, ghost.Lifetime));

                if (ghost.Rt == null || ghost.Img == null || t >= 1f)
                {
                    if (ghost.Rt != null) Destroy(ghost.Rt.gameObject);
                    _afterimages.RemoveAt(i);
                    continue;
                }

                Color c = ghost.StartColor;
                c.a = ghost.StartColor.a * (1f - t) * (1f - t);
                ghost.Img.color = c;
                ghost.Rt.localScale = ghost.StartScale * Mathf.Lerp(0.96f, 0.72f, t);
            }
        }

        private void UpdateSparkles(float dt)
        {
            for (int i = _sparkles.Count - 1; i >= 0; i--)
            {
                SparkleMote s = _sparkles[i];
                s.Elapsed += dt;
                float t = Mathf.Clamp01(s.Elapsed / Mathf.Max(0.01f, s.Lifetime));

                if (s.Rt == null || s.Img == null || t >= 1f)
                {
                    if (s.Rt != null) Destroy(s.Rt.gameObject);
                    _sparkles.RemoveAt(i);
                    continue;
                }

                s.Rt.anchoredPosition += s.Velocity * dt;
                s.Velocity *= Mathf.Pow(0.18f, dt);
                float scale = s.StartScale * Mathf.Sin(t * Mathf.PI);
                s.Rt.sizeDelta = new Vector2(scale, scale);
                s.Rt.Rotate(0f, 0f, 200f * dt);

                Color c = s.BaseColor;
                c.a = s.BaseColor.a * (1f - t);
                s.Img.color = c;
            }
        }

        private void UpdateImpactRings(float dt)
        {
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                ImpactRing ring = _rings[i];
                ring.Elapsed += dt;
                float t = Mathf.Clamp01(ring.Elapsed / Mathf.Max(0.01f, ring.Duration));

                if (ring.Rt == null || ring.Img == null || t >= 1f)
                {
                    if (ring.Rt != null) Destroy(ring.Rt.gameObject);
                    _rings.RemoveAt(i);
                    continue;
                }

                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float size = Mathf.Lerp(ring.StartSize, ring.EndSize, eased);
                ring.Rt.sizeDelta = new Vector2(size, size);

                Color c = ring.BaseColor;
                c.a = ring.BaseColor.a * (1f - t);
                ring.Img.color = c;
            }
        }

        private void SpawnAfterimage(Sprite cardSprite, Vector2 localPos, Quaternion rot, Vector3 scale, float lifetime)
        {
            if (_trailsContainer == null) return;

            GameObject go = new("ShuffleGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(_trailsContainer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(96f, 134f);
            rt.anchoredPosition = localPos;
            rt.localRotation = rot;
            rt.localScale = scale * 0.96f;

            Image img = go.GetComponent<Image>();
            img.sprite = cardSprite;
            img.color = _afterimageTint;
            img.raycastTarget = false;

            _afterimages.Add(new CardAfterimage
            {
                Rt = rt,
                Img = img,
                Elapsed = 0f,
                Lifetime = lifetime,
                StartColor = _afterimageTint,
                StartScale = scale
            });
        }

        private void SpawnSparkle(Vector2 localPos, Vector2 velocity, float size, float lifetime)
        {
            if (_cardsContainer == null) return;
            GameObject go = new("ShuffleSparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(_cardsContainer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = localPos;
            rt.sizeDelta = new Vector2(size, size);

            Image img = go.GetComponent<Image>();
            img.sprite = GetProceduralSparkleSprite();
            img.color = _sparkleColor;
            img.raycastTarget = false;

            _sparkles.Add(new SparkleMote
            {
                Rt = rt,
                Img = img,
                Velocity = velocity,
                Elapsed = 0f,
                Lifetime = lifetime,
                StartScale = size,
                BaseColor = _sparkleColor
            });
        }

        private void SpawnImpactRing(Vector2 localPos, float startSize, float endSize, float duration, Color color)
        {
            if (_cardsContainer == null) return;
            GameObject go = new("ShuffleImpactRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(_cardsContainer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = localPos;
            rt.sizeDelta = new Vector2(startSize, startSize);

            Image img = go.GetComponent<Image>();
            img.sprite = GetProceduralRingSprite();
            img.color = color;
            img.raycastTarget = false;

            _rings.Add(new ImpactRing
            {
                Rt = rt,
                Img = img,
                Elapsed = 0f,
                Duration = duration,
                StartSize = startSize,
                EndSize = endSize,
                BaseColor = color
            });
        }

        private static void PunchPileTransform(Transform target, float targetScale, float duration, float rotKick)
        {
            if (target == null) return;
            target.DOKill();
            target.localScale = Vector3.one;
            target.localRotation = Quaternion.identity;

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(target.DOScale(Vector3.one * targetScale, duration * 0.42f).SetEase(Ease.OutQuad));
            seq.Join(target.DOLocalRotate(new Vector3(0f, 0f, UnityEngine.Random.Range(-rotKick, rotKick)), duration * 0.42f));
            seq.Append(target.DOScale(Vector3.one, duration * 0.58f).SetEase(Ease.OutBack));
            seq.Join(target.DOLocalRotate(Vector3.zero, duration * 0.58f).SetEase(Ease.OutQuad));
        }

        private Vector2 ResolveCanvasLocalPosition(Transform target, Vector2 fallback)
        {
            if (target == null || _layerRoot == null) return fallback;

            Camera uiCam = _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _rootCanvas.worldCamera
                : null;

            Vector2 screenPt;
            if (target is RectTransform rt)
            {
                Vector3 worldCenter = rt.TransformPoint(rt.rect.center);
                screenPt = RectTransformUtility.WorldToScreenPoint(uiCam, worldCenter);
            }
            else
            {
                Camera worldCam = Camera.main;
                if (worldCam == null) return fallback;
                screenPt = worldCam.WorldToScreenPoint(target.position);
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_layerRoot, screenPt, uiCam, out Vector2 localPt))
            {
                return localPt;
            }

            return fallback;
        }

        private static Vector2 EvaluateQuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return (u * u) * a + (2f * u * t) * b + (t * t) * c;
        }

        private void ClearAllVisuals()
        {
            for (int i = 0; i < _flyingCards.Count; i++)
            {
                if (_flyingCards[i].CardRt != null) Destroy(_flyingCards[i].CardRt.gameObject);
                if (_flyingCards[i].Trail != null) Destroy(_flyingCards[i].Trail.gameObject);
            }
            _flyingCards.Clear();

            for (int i = 0; i < _afterimages.Count; i++)
            {
                if (_afterimages[i].Rt != null) Destroy(_afterimages[i].Rt.gameObject);
            }
            _afterimages.Clear();

            for (int i = 0; i < _sparkles.Count; i++)
            {
                if (_sparkles[i].Rt != null) Destroy(_sparkles[i].Rt.gameObject);
            }
            _sparkles.Clear();

            for (int i = 0; i < _rings.Count; i++)
            {
                if (_rings[i].Rt != null) Destroy(_rings[i].Rt.gameObject);
            }
            _rings.Clear();
        }

        #region Procedural Cached Sprites
        private static Sprite GetProceduralCardBackSprite()
        {
            if (_proceduralCardBack != null) return _proceduralCardBack;

            const int w = 96;
            const int h = 132;
            Texture2D tex = new(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[w * h];

            Color darkOuter = new(0.10f, 0.08f, 0.14f, 1f);
            Color deepBody = new(0.18f, 0.14f, 0.24f, 1f);
            Color goldBright = new(0.96f, 0.80f, 0.40f, 1f);
            Color goldWarm = new(0.76f, 0.56f, 0.22f, 1f);
            Color starCore = new(1f, 0.96f, 0.82f, 1f);

            float cx = (w - 1) * 0.5f;
            float cy = (h - 1) * 0.5f;
            float cornerRadius = 11f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - cx) - (cx - cornerRadius);
                    float dy = Mathf.Abs(y - cy) - (cy - cornerRadius);
                    float outsideDist = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)) + Mathf.Min(Mathf.Max(dx, dy), 0f) - cornerRadius;

                    if (outsideDist > 1f)
                    {
                        pixels[y * w + x] = Color.clear;
                        continue;
                    }

                    float edgeAlpha = Mathf.Clamp01(0.8f - outsideDist);
                    float inset = -outsideDist;

                    float grad = (x / (float)w * 0.4f + y / (float)h * 0.6f);
                    Color col = Color.Lerp(darkOuter, deepBody, grad);

                    if (inset < 2.2f)
                    {
                        col = darkOuter;
                    }
                    else if (inset < 6.0f)
                    {
                        col = Color.Lerp(goldWarm, goldBright, (inset - 2.2f) / 3.8f);
                    }
                    else if (inset < 8.0f)
                    {
                        col = darkOuter;
                    }
                    else if (inset > 11.5f && inset < 13.4f)
                    {
                        col = goldWarm;
                    }
                    else
                    {
                        float nx = (x - cx) / 22f;
                        float ny = (y - cy) / 28f;
                        float r = Mathf.Sqrt(nx * nx + ny * ny);
                        float star = Mathf.Pow(Mathf.Abs(nx), 0.58f) + Mathf.Pow(Mathf.Abs(ny), 0.58f);

                        if (star < 0.78f)
                        {
                            col = Color.Lerp(starCore, goldBright, star / 0.78f);
                        }
                        else if (Mathf.Abs(r - 0.92f) < 0.08f)
                        {
                            col = Color.Lerp(col, goldWarm, 0.75f);
                        }
                    }

                    col.a *= edgeAlpha;
                    pixels[y * w + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _proceduralCardBack = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return _proceduralCardBack;
        }

        private static Sprite GetProceduralSparkleSprite()
        {
            if (_proceduralSparkle != null) return _proceduralSparkle;

            const int size = 48;
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;

            Color rimGold = new(0.95f, 0.56f, 0.10f, 1f);
            Color coreWhite = new(1f, 0.99f, 0.92f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = Mathf.Abs(x - half) / half;
                    float ny = Mathf.Abs(y - half) / half;
                    float radial = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny));
                    float star = Mathf.Clamp01(1f - (Mathf.Pow(nx, 0.48f) + Mathf.Pow(ny, 0.48f)));
                    float a = Mathf.Clamp01(star * 1.45f + radial * radial * 0.55f);
                    Color c = Color.Lerp(rimGold, coreWhite, Mathf.Clamp01(star * 1.6f));
                    c.a = a;
                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _proceduralSparkle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _proceduralSparkle;
        }

        private static Sprite GetProceduralRingSprite()
        {
            if (_proceduralRing != null) return _proceduralRing;

            const int size = 128;
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float band = Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.12f, 2f));
                    float innerSoft = Mathf.Clamp01(1f - r) * 0.16f;
                    float a = r > 1f ? 0f : Mathf.Clamp01(band + innerSoft);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _proceduralRing = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _proceduralRing;
        }
        #endregion
    }
}
