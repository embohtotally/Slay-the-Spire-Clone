using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI;
using PlushieOdyssey.UI.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PlushieOdyssey.Vfx
{
    /// <summary>
    /// Centralized procedural combat game-feel director (auto-attached by ActionSystem).
    /// Provides impact hit-stop, directional camera micro-shake & zoom punch, played-card
    /// ribbon/orb flight trails to targets, Boss Entrance banners, Will threshold alerts,
    /// and Slay-the-Spire combat keyboard shortcuts (E = End Turn, A = Draw, D = Discard).
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatJuiceDirector : Singleton<CombatJuiceDirector>
    {
        public const string ScreenShakePrefKey = "Settings.ScreenShake";

        private static Sprite s_solidSprite;
        private static Sprite s_glowSprite;

        private Camera _mainCam;
        private Vector3 _camBaseLocalPos;
        private float _camBaseOrthoSize;
        private bool _camBaseCaptured;
        private Coroutine _hitStopRoutine;
        private Coroutine _shakeRoutine;
        private float _preHitStopTimeScale = 1f;
        private bool _isHitStopped;

        private Canvas _overlayCanvas;
        private RectTransform _fxLayer;
        private Image _willVignette;
        private RectTransform _bannerRoot;
        private Image _bannerBg;
        private TextMeshProUGUI _bannerTitle;
        private TextMeshProUGUI _bannerSub;
        private Sequence _bannerSeq;

        private bool _hadHighWill;
        private bool _hadBreakdown;
        private bool _willStateInitialized;
        private bool _bossIntroChecked;

        public static bool ScreenShakeEnabled
        {
            get => PlayerPrefs.GetInt(ScreenShakePrefKey, 1) != 0;
            set => PlayerPrefs.SetInt(ScreenShakePrefKey, value ? 1 : 0);
        }

        private void OnEnable()
        {
            ActionSystem.SubscribeReaction<PlayCardGA>(OnPlayCardPre, ReactionTiming.PRE);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPre, ReactionTiming.PRE);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);
        }

        private void OnDisable()
        {
            ActionSystem.UnsubscribeReaction<PlayCardGA>(OnPlayCardPre, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPre, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);
            RestoreTimeScaleIfHitStopped();
            RestoreCameraBase();
        }

        private void Start()
        {
            CaptureCameraBase();
            EnsureOverlayLayer();
            StartCoroutine(DelayedBossIntroCheck());
        }

        private void Update()
        {
            MonitorWillThresholdTransitions();
            HandleCombatHotkeys();
        }

        // ------------------------------------------------------------------
        // Public Juice Triggers
        // ------------------------------------------------------------------
        public void TriggerHitStop(float durationSeconds)
        {
            if (durationSeconds <= 0.005f) return;
            // Never interfere with Pause menu (Time.timeScale == 0 while not already hit-stopped)
            if (!_isHitStopped && Time.timeScale <= 0.01f) return;

            if (_hitStopRoutine != null)
            {
                StopCoroutine(_hitStopRoutine);
            }
            _hitStopRoutine = StartCoroutine(HitStopRoutine(Mathf.Clamp(durationSeconds, 0.015f, 0.09f)));
        }

        public void TriggerCameraShake(float strength, float duration = 0.18f, Vector2? directionalBias = null)
        {
            if (!ScreenShakeEnabled || strength <= 0.001f) return;
            CaptureCameraBase();
            if (_mainCam == null) return;

            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                RestoreCameraBase();
            }
            _shakeRoutine = StartCoroutine(CameraShakeRoutine(strength, duration, directionalBias ?? Vector2.zero));
        }

        public void TriggerCameraZoomPunch(float deltaOrtho = -0.18f, float duration = 0.22f)
        {
            if (!ScreenShakeEnabled) return;
            CaptureCameraBase();
            if (_mainCam == null || !_mainCam.orthographic) return;

            _mainCam.DOKill();
            float punchTarget = Mathf.Max(1f, _camBaseOrthoSize + deltaOrtho);
            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(_mainCam.DOOrthoSize(punchTarget, duration * 0.35f).SetEase(Ease.OutQuad));
            seq.Append(_mainCam.DOOrthoSize(_camBaseOrthoSize, duration * 0.65f).SetEase(Ease.OutBack));
        }

        public void OnDamageImpact(CombatantView target, CombatantView caster, int rawAmount, int hpLost, int shieldAbsorbed, bool shieldBroken, bool killed)
        {
            if (target == null) return;

            bool isHeavy = hpLost >= 12 || rawAmount >= 14;
            Vector2 dir = caster != null
                ? ((Vector2)(target.transform.position - caster.transform.position)).normalized
                : Vector2.right;

            if (killed)
            {
                TriggerHitStop(0.062f);
                TriggerCameraShake(0.28f, 0.26f, dir);
                TriggerCameraZoomPunch(-0.22f, 0.26f);
                SpawnWorldImpactBurst(target.transform.position, new Color(1f, 0.38f, 0.26f, 0.95f), 1.45f);
            }
            else if (shieldBroken)
            {
                TriggerHitStop(0.052f);
                TriggerCameraShake(0.22f, 0.22f, dir);
                SpawnWorldImpactBurst(target.transform.position, new Color(0.42f, 0.88f, 1f, 0.95f), 1.25f);
            }
            else if (isHeavy)
            {
                TriggerHitStop(0.038f);
                TriggerCameraShake(0.18f, 0.19f, dir);
                SpawnWorldImpactBurst(target.transform.position, new Color(1f, 0.72f, 0.32f, 0.90f), 1.15f);
            }
            else if (hpLost > 0)
            {
                TriggerCameraShake(0.09f, 0.13f, dir);
                SpawnWorldImpactBurst(target.transform.position, new Color(1f, 0.82f, 0.50f, 0.78f), 0.85f);
            }
            else if (shieldAbsorbed > 0)
            {
                TriggerCameraShake(0.06f, 0.10f, dir);
                SpawnWorldImpactBurst(target.transform.position, new Color(0.50f, 0.90f, 1f, 0.85f), 0.92f);
            }
        }

        public void ShowCombatBanner(string title, string subtitle, Color accentColor, float holdSeconds = 1.15f)
        {
            EnsureOverlayLayer();
            if (_bannerRoot == null) return;

            _bannerSeq?.Kill();
            _bannerRoot.gameObject.SetActive(true);
            _bannerRoot.SetAsLastSibling();
            _bannerTitle.text = title;
            _bannerTitle.color = accentColor;
            _bannerSub.text = subtitle ?? string.Empty;
            _bannerSub.gameObject.SetActive(!string.IsNullOrWhiteSpace(subtitle));

            CanvasGroup cg = _bannerRoot.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            _bannerRoot.localScale = new Vector3(1.18f, 0.86f, 1f);

            _bannerSeq = DOTween.Sequence().SetUpdate(true);
            _bannerSeq.Append(cg.DOFade(1f, 0.18f));
            _bannerSeq.Join(_bannerRoot.DOScale(Vector3.one, 0.24f).SetEase(Ease.OutBack));
            _bannerSeq.AppendInterval(holdSeconds);
            _bannerSeq.Append(cg.DOFade(0f, 0.26f).SetEase(Ease.InQuad));
            _bannerSeq.Join(_bannerRoot.DOScale(new Vector3(0.95f, 0.95f, 1f), 0.26f));
            _bannerSeq.OnComplete(() => { if (_bannerRoot != null) _bannerRoot.gameObject.SetActive(false); });
        }

        // ------------------------------------------------------------------
        // Card Play Cast Trail + Hero Action Reaction
        // ------------------------------------------------------------------
        private void OnPlayCardPre(PlayCardGA ga)
        {
            if (ga == null || ga.Card == null) return;
            EnsureOverlayLayer();

            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            CombatantView target = ga.ManualTarget;
            if (target == null && ga.Card.Type == CardType.Attack && EnemySystem.Instance != null && EnemySystem.Instance.Enemies.Count > 0)
            {
                target = EnemySystem.Instance.Enemies[0];
            }

            if (hero != null)
            {
                if (ga.Card.Type == CardType.Attack)
                {
                    Vector3 dest = target != null ? target.transform.position : hero.transform.position + Vector3.right * 2f;
                    hero.PlayProceduralAttackLunge(dest, 0.24f);
                }
                else
                {
                    hero.PlayProceduralSupportPulse();
                }
            }

            // Spawn crisp glowing energy orb arcing from hand zone to target (or Hero for Buff/Power)
            CombatantView trailDest = ga.Card.Type == CardType.Attack && target != null ? target : hero;
            if (trailDest != null)
            {
                Color trailColor = ga.Card.Type switch
                {
                    CardType.Attack => new Color(1f, 0.56f, 0.28f, 0.95f),
                    CardType.Power => new Color(0.76f, 0.48f, 1f, 0.95f),
                    _ => new Color(0.42f, 0.90f, 1f, 0.95f)
                };
                SpawnCardCastArcToWorld(trailDest.transform.position, trailColor);
            }
        }

        private void OnEnemyTurnPre(EnemyTurnGA _)
        {
            // Subtle tension pulse when enemy turn begins
        }

        private void OnEnemyTurnPost(EnemyTurnGA _)
        {
            // Player turn restored
        }

        private void SpawnCardCastArcToWorld(Vector3 targetWorldPos, Color color)
        {
            EnsureOverlayLayer();
            if (_fxLayer == null) return;
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return;

            Vector2 endLocal = WorldToLayerLocal(targetWorldPos);
            Vector2 startLocal = new(0f, -_fxLayer.rect.height * 0.34f);
            Vector2 midLocal = (startLocal + endLocal) * 0.5f + new Vector2(UnityEngine.Random.Range(-70f, 70f), 110f);

            GameObject orbGo = new("CardCastOrb", typeof(RectTransform), typeof(Image));
            orbGo.layer = _fxLayer.gameObject.layer;
            RectTransform rt = (RectTransform)orbGo.transform;
            rt.SetParent(_fxLayer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(46f, 46f);
            rt.anchoredPosition = startLocal;

            Image img = orbGo.GetComponent<Image>();
            img.sprite = GetGlowSprite();
            img.color = color;
            img.raycastTarget = false;

            // Attach native Canvas ribbon trail for juicy Slay-the-Spire streak!
            GameObject trailGo = new("CastTrail", typeof(RectTransform), typeof(UIProceduralRibbonTrail));
            trailGo.layer = _fxLayer.gameObject.layer;
            RectTransform trRt = (RectTransform)trailGo.transform;
            trRt.SetParent(_fxLayer, false);
            trRt.anchorMin = trRt.anchorMax = new Vector2(0.5f, 0.5f);
            trRt.anchoredPosition = Vector2.zero;
            trRt.sizeDelta = Vector2.zero;
            UIProceduralRibbonTrail ribbon = trailGo.GetComponent<UIProceduralRibbonTrail>();
            ribbon.raycastTarget = false;
            ribbon.Configure(
                headWidth: 26f,
                lifetime: 0.22f,
                headColor: Color.white,
                midColor: color,
                tailColor: new Color(color.r, color.g, color.b, 0f),
                outerGlowColor: new Color(color.r * 0.45f, color.g * 0.35f, color.b * 0.65f, 0.55f));

            StartCoroutine(AnimateCastOrbRoutine(rt, img, ribbon, startLocal, midLocal, endLocal, color));
        }

        private IEnumerator AnimateCastOrbRoutine(RectTransform orbRt, Image orbImg, UIProceduralRibbonTrail ribbon, Vector2 p0, Vector2 p1, Vector2 p2, Color color)
        {
            const float dur = 0.20f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (orbRt == null) break;
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / dur);
                float ease = 1f - (1f - t) * (1f - t);
                Vector2 pos = QuadraticBezier(p0, p1, p2, ease);
                orbRt.anchoredPosition = pos;
                orbRt.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.35f, t);
                ribbon?.AddPoint(pos);
                ribbon?.Tick(dt);
                yield return null;
            }

            if (orbRt != null) Destroy(orbRt.gameObject);
            SpawnLayerImpactRing(p2, color, 1.0f);

            float tailElapsed = 0f;
            while (tailElapsed < 0.24f && ribbon != null)
            {
                float dt = Time.unscaledDeltaTime;
                tailElapsed += dt;
                ribbon.Tick(dt);
                yield return null;
            }
            if (ribbon != null) Destroy(ribbon.gameObject);
        }

        private void SpawnWorldImpactBurst(Vector3 worldPos, Color color, float scale)
        {
            EnsureOverlayLayer();
            if (_fxLayer == null) return;
            Vector2 local = WorldToLayerLocal(worldPos);
            SpawnLayerImpactRing(local, color, scale);
        }

        private void SpawnLayerImpactRing(Vector2 localPos, Color color, float scale)
        {
            if (_fxLayer == null) return;

            GameObject go = new("ImpactBurst", typeof(RectTransform), typeof(Image));
            go.layer = _fxLayer.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(_fxLayer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = localPos;
            rt.sizeDelta = new Vector2(92f, 92f);
            rt.localScale = Vector3.one * 0.35f;

            Image img = go.GetComponent<Image>();
            img.sprite = GetGlowSprite();
            img.color = color;
            img.raycastTarget = false;

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Join(rt.DOScale(scale * 1.65f, 0.22f).SetEase(Ease.OutCubic));
            seq.Join(img.DOFade(0f, 0.24f).SetEase(Ease.OutQuad));
            seq.OnComplete(() => { if (go != null) Destroy(go); });
        }

        // ------------------------------------------------------------------
        // Boss Entrance & Will Threshold Callouts
        // ------------------------------------------------------------------
        private IEnumerator DelayedBossIntroCheck()
        {
            yield return null;
            yield return null;
            if (_bossIntroChecked) yield break;
            _bossIntroChecked = true;

            if (EnemySystem.Instance == null || EnemySystem.Instance.Enemies == null || EnemySystem.Instance.Enemies.Count == 0)
                yield break;

            bool isBossNode = RunManager.Instance != null && RunManager.Instance.IsCurrentNodeBoss;

            EnemyView bossCandidate = null;
            foreach (EnemyView e in EnemySystem.Instance.Enemies)
            {
                if (e != null && (isBossNode || e.MaxHealth >= 90))
                {
                    bossCandidate = e;
                    break;
                }
            }

            if (bossCandidate != null)
            {
                TriggerCameraShake(0.22f, 0.35f);
                TriggerCameraZoomPunch(-0.26f, 0.38f);
                string rawName = bossCandidate.Data != null ? bossCandidate.Data.name : bossCandidate.name;
                string bossName = !string.IsNullOrWhiteSpace(rawName) ? rawName.ToUpper() : "CHAPTER BOSS";
                ShowCombatBanner(bossName, "BOSS ENCOUNTER", new Color(1f, 0.42f, 0.34f, 1f), 1.45f);
            }
        }

        private void MonitorWillThresholdTransitions()
        {
            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            if (hero == null) return;

            bool isHighWill = hero.IsHighWill;
            bool isBreakdown = hero.IsLowWill;

            if (!_willStateInitialized)
            {
                _hadHighWill = isHighWill;
                _hadBreakdown = isBreakdown;
                _willStateInitialized = true;
                UpdateWillVignette(isHighWill, isBreakdown);
                return;
            }

            if (isHighWill != _hadHighWill || isBreakdown != _hadBreakdown)
            {
                if (isBreakdown && !_hadBreakdown)
                {
                    TriggerCameraShake(0.20f, 0.28f);
                    ShowCombatBanner("- LOW WILL!", "Will < 25% — Attack cards sealed & Max HP reduced until Will recovers", new Color(0.86f, 0.38f, 1f, 1f), 1.35f);
                }
                else if (isHighWill && !_hadHighWill)
                {
                    int bonusPct = Mathf.RoundToInt((hero.HighWillBonusMultiplier - 1f) * 100f);
                    ShowCombatBanner("+ HIGH WILL!", $"Will >= 75% — +{bonusPct}% Attack & Shield bonus active", new Color(1f, 0.88f, 0.40f, 1f), 1.2f);
                }

                _hadHighWill = isHighWill;
                _hadBreakdown = isBreakdown;
                UpdateWillVignette(isHighWill, isBreakdown);
            }
        }

        private void UpdateWillVignette(bool highWill, bool breakdown)
        {
            if (_willVignette == null) return;
            _willVignette.DOKill();
            if (breakdown)
            {
                _willVignette.DOColor(new Color(0.45f, 0.08f, 0.56f, 0.24f), 0.35f).SetUpdate(true);
            }
            else if (highWill)
            {
                _willVignette.DOColor(new Color(1f, 0.86f, 0.36f, 0.08f), 0.35f).SetUpdate(true);
            }
            else
            {
                _willVignette.DOColor(new Color(0f, 0f, 0f, 0f), 0.35f).SetUpdate(true);
            }
        }

        // ------------------------------------------------------------------
        // Combat Keyboard Shortcuts (E = End Turn, A = Draw Pile, D = Discard Pile)
        // ------------------------------------------------------------------
        private void HandleCombatHotkeys()
        {
            if (Keyboard.current == null || Time.timeScale <= 0.01f) return;

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                EndTurnButtonUI endBtn = FindFirstObjectByType<EndTurnButtonUI>();
                if (endBtn != null && endBtn.isActiveAndEnabled)
                {
                    Button b = endBtn.GetComponent<Button>();
                    if (b != null && b.interactable) b.onClick.Invoke();
                }
            }
            else if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                CombatPileViewerUI viewer = FindFirstObjectByType<CombatPileViewerUI>();
                if (viewer != null) viewer.ToggleDrawPile();
            }
            else if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                CombatPileViewerUI viewer = FindFirstObjectByType<CombatPileViewerUI>();
                if (viewer != null) viewer.ToggleDiscardPile();
            }
        }

        // ------------------------------------------------------------------
        // Coroutines & Overlay Construction
        // ------------------------------------------------------------------
        private IEnumerator HitStopRoutine(float duration)
        {
            if (!_isHitStopped)
            {
                _preHitStopTimeScale = Time.timeScale > 0.01f ? Time.timeScale : 1f;
                _isHitStopped = true;
            }
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            RestoreTimeScaleIfHitStopped();
            _hitStopRoutine = null;
        }

        private void RestoreTimeScaleIfHitStopped()
        {
            if (!_isHitStopped) return;
            _isHitStopped = false;
            if (Time.timeScale > 0f)
            {
                Time.timeScale = _preHitStopTimeScale > 0.01f ? _preHitStopTimeScale : 1f;
            }
        }

        private IEnumerator CameraShakeRoutine(float strength, float duration, Vector2 bias)
        {
            float elapsed = 0f;
            while (elapsed < duration && _mainCam != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float damp = 1f - Mathf.Clamp01(elapsed / duration);
                float mag = strength * damp * damp;
                Vector2 rnd = UnityEngine.Random.insideUnitCircle * mag;
                Vector2 kick = bias * (mag * 0.45f * Mathf.Sin((1f - damp) * Mathf.PI));
                _mainCam.transform.localPosition = _camBaseLocalPos + new Vector3(rnd.x + kick.x, rnd.y + kick.y, 0f);
                yield return null;
            }
            RestoreCameraBase();
            _shakeRoutine = null;
        }

        private void CaptureCameraBase()
        {
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam != null && !_camBaseCaptured)
            {
                _camBaseLocalPos = _mainCam.transform.localPosition;
                _camBaseOrthoSize = _mainCam.orthographicSize;
                _camBaseCaptured = true;
            }
        }

        private void RestoreCameraBase()
        {
            if (_mainCam != null && _camBaseCaptured)
            {
                _mainCam.transform.localPosition = _camBaseLocalPos;
                if (_mainCam.orthographic) _mainCam.orthographicSize = _camBaseOrthoSize;
            }
        }

        private void EnsureOverlayLayer()
        {
            if (_fxLayer != null) return;
            _overlayCanvas = FindFirstObjectByType<Canvas>();
            if (_overlayCanvas == null) return;
            _overlayCanvas = _overlayCanvas.rootCanvas;

            int layer = _overlayCanvas.gameObject.layer;
            GameObject rootGo = new("CombatJuiceOverlay", typeof(RectTransform));
            rootGo.layer = layer;
            _fxLayer = (RectTransform)rootGo.transform;
            _fxLayer.SetParent(_overlayCanvas.transform, false);
            _fxLayer.anchorMin = Vector2.zero;
            _fxLayer.anchorMax = Vector2.one;
            _fxLayer.offsetMin = Vector2.zero;
            _fxLayer.offsetMax = Vector2.zero;

            // Subtle Will screen tint edge overlay
            GameObject vigGo = new("WillStateOverlay", typeof(RectTransform), typeof(Image));
            vigGo.layer = layer;
            RectTransform vigRt = (RectTransform)vigGo.transform;
            vigRt.SetParent(_fxLayer, false);
            vigRt.anchorMin = Vector2.zero;
            vigRt.anchorMax = Vector2.one;
            vigRt.offsetMin = Vector2.zero;
            vigRt.offsetMax = Vector2.zero;
            _willVignette = vigGo.GetComponent<Image>();
            _willVignette.sprite = GetSolidSprite();
            _willVignette.color = new Color(0f, 0f, 0f, 0f);
            _willVignette.raycastTarget = false;

            // Center Boss / Will Banner
            GameObject bGo = new("JuiceBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            bGo.layer = layer;
            _bannerRoot = (RectTransform)bGo.transform;
            _bannerRoot.SetParent(_fxLayer, false);
            _bannerRoot.anchorMin = _bannerRoot.anchorMax = new Vector2(0.5f, 0.72f);
            _bannerRoot.pivot = new Vector2(0.5f, 0.5f);
            _bannerRoot.sizeDelta = new Vector2(780f, 118f);
            CanvasGroup bCg = bGo.GetComponent<CanvasGroup>();
            bCg.blocksRaycasts = false;
            bCg.interactable = false;
            _bannerBg = bGo.GetComponent<Image>();
            _bannerBg.sprite = GetSolidSprite();
            _bannerBg.color = new Color(0.08f, 0.05f, 0.04f, 0.84f);
            _bannerBg.raycastTarget = false;

            GameObject titleGo = new("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.layer = layer;
            RectTransform tRt = (RectTransform)titleGo.transform;
            tRt.SetParent(_bannerRoot, false);
            tRt.anchorMin = new Vector2(0f, 0.38f);
            tRt.anchorMax = new Vector2(1f, 0.94f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            _bannerTitle = titleGo.GetComponent<TextMeshProUGUI>();
            _bannerTitle.fontSize = 38f;
            _bannerTitle.fontStyle = FontStyles.Bold;
            _bannerTitle.alignment = TextAlignmentOptions.Center;
            _bannerTitle.raycastTarget = false;

            GameObject subGo = new("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.layer = layer;
            RectTransform sRt = (RectTransform)subGo.transform;
            sRt.SetParent(_bannerRoot, false);
            sRt.anchorMin = new Vector2(0f, 0.08f);
            sRt.anchorMax = new Vector2(1f, 0.42f);
            sRt.offsetMin = Vector2.zero;
            sRt.offsetMax = Vector2.zero;
            _bannerSub = subGo.GetComponent<TextMeshProUGUI>();
            _bannerSub.fontSize = 20f;
            _bannerSub.color = new Color(0.94f, 0.90f, 0.82f, 0.95f);
            _bannerSub.alignment = TextAlignmentOptions.Center;
            _bannerSub.raycastTarget = false;

            _bannerRoot.gameObject.SetActive(false);
        }

        private Vector2 WorldToLayerLocal(Vector3 worldPos)
        {
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null || _fxLayer == null) return Vector2.zero;
            Vector2 screen = _mainCam.WorldToScreenPoint(worldPos);
            Camera uiCam = _overlayCanvas != null && _overlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _overlayCanvas.worldCamera
                : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_fxLayer, screen, uiCam, out Vector2 local);
            return local;
        }

        private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private static Sprite GetSolidSprite()
        {
            if (s_solidSprite != null) return s_solidSprite;
            Texture2D tex = Texture2D.whiteTexture;
            s_solidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return s_solidSprite;
        }

        private static Sprite GetGlowSprite()
        {
            if (s_glowSprite != null) return s_glowSprite;
            const int size = 64;
            Texture2D tex = new(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float half = (size - 1) * 0.5f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float norm = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half);
                    float a = Mathf.Pow(1f - norm, 2.0f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            s_glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return s_glowSprite;
        }
    }
}