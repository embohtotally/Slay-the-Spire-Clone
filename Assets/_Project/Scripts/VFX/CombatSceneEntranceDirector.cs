using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.UI.Combat;
using UnityEngine;

namespace PlushieOdyssey.Vfx
{
    /// <summary>
    /// Choreographs a staggered, juicy entrance when entering Battle (Game.unity) so elements
    /// cascade onto the battlefield in rhythm right as the procedural blink transition opens,
    /// instead of everything popping on screen at once:
    ///   1. Top Bar glides down from above.
    ///   2. Hero hops onto the battlefield from the left.
    ///   3. Enemies swoop in one by one staggered from the right.
    ///   4. Bottom Combat HUD (Mana, Draw Pile, Discard Pile, End Turn, Perks) pops in left-to-right.
    ///   5. Enemy Intent badges pop in staggered above each enemy.
    ///   6. Opening Turn 1 banner & opening hand cards deal out.
    /// Auto-attached by ActionSystem.Awake() so zero manual scene wiring is required.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatSceneEntranceDirector : Singleton<CombatSceneEntranceDirector>
    {
        private sealed class StagedUiItem
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 HomeAnchoredPos;
            public Vector3 HomeScale;
            public float OriginalAlpha;
            public bool AddedCanvasGroup;
        }

        private sealed class StagedCombatant
        {
            public CombatantView View;
            public Vector3 HomeLocalPos;
            public Vector3 HomeLocalScale;
        }

        private readonly List<StagedUiItem> _stagedUi = new();
        private readonly List<StagedCombatant> _stagedEnemies = new();
        private StagedUiItem _stagedTopBar;
        private StagedCombatant _stagedHero;
        private Sequence _entranceSeq;
        private bool _isStagingEntrance;
        private bool _hasCompletedEntrance;

        public bool IsStagingEntrance => _isStagingEntrance;
        public bool HasCompletedEntrance => _hasCompletedEntrance;

        public bool TryGetStagedHomeScale(Transform target, out Vector3 homeScale)
        {
            if (target != null)
            {
                if (_stagedTopBar != null && _stagedTopBar.Rect == target)
                {
                    homeScale = _stagedTopBar.HomeScale;
                    return true;
                }
                for (int i = 0; i < _stagedUi.Count; i++)
                {
                    if (_stagedUi[i] != null && _stagedUi[i].Rect == target)
                    {
                        homeScale = _stagedUi[i].HomeScale;
                        return true;
                    }
                }
            }
            homeScale = Vector3.one;
            return false;
        }

        private void OnDisable()
        {
            KillAndSnapToHome();
        }

        /// <summary>
        /// Stages all battlefield actors & HUD off-stage immediately, waits for the BlinkSceneTransition
        /// to begin parting open, and then animates every layer in staggered sequence before invoking
        /// <paramref name="onReadyToStartTurn"/> (CombatStartedGA + opening hand draw).
        /// </summary>
        public void BeginBattleEntrance(Action onReadyToStartTurn)
        {
            _isStagingEntrance = true;
            _hasCompletedEntrance = false;

            StageSceneElementsHidden();

            BlinkSceneTransition.RunWhenRevealing(this, () =>
            {
                if (this == null || !isActiveAndEnabled)
                {
                    onReadyToStartTurn?.Invoke();
                    return;
                }
                StartCoroutine(PlayStaggeredEntranceRoutine(onReadyToStartTurn));
            });
        }

        private void StageSceneElementsHidden()
        {
            _stagedUi.Clear();
            _stagedEnemies.Clear();
            _stagedTopBar = null;
            _stagedHero = null;

            Canvas gameCanvas = ResolveCombatCanvas();
            if (gameCanvas != null)
            {
                // 1) Top Bar glides down from above
                Transform topBarTf = gameCanvas.transform.Find("CombatTopBar");
                if (topBarTf is RectTransform topBarRt && topBarRt.gameObject.activeInHierarchy)
                {
                    _stagedTopBar = CaptureUi(topBarRt);
                    _stagedTopBar.Rect.anchoredPosition = _stagedTopBar.HomeAnchoredPos + new Vector2(0f, 118f);
                    _stagedTopBar.Group.alpha = 0f;
                }

                // 2) Bottom HUD items pop in staggered left-to-right
                string[] bottomHudNames =
                {
                    "Mana UI",
                    "DrawPointVisual",
                    "Deck Size UI",
                    "Perks UI",
                    "Discard Pile UI",
                    "End Turn Button"
                };

                foreach (string hudName in bottomHudNames)
                {
                    Transform tf = gameCanvas.transform.Find(hudName);
                    if (tf is RectTransform rt && rt.gameObject.activeInHierarchy)
                    {
                        StagedUiItem item = CaptureUi(rt);
                        item.Rect.anchoredPosition = item.HomeAnchoredPos + new Vector2(0f, -44f);
                        item.Rect.localScale = item.HomeScale * 0.45f;
                        item.Group.alpha = 0f;
                        _stagedUi.Add(item);
                    }
                }
            }

            // 3) Hero starts off-stage to the left
            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            if (hero != null)
            {
                _stagedHero = new StagedCombatant
                {
                    View = hero,
                    HomeLocalPos = hero.transform.localPosition,
                    HomeLocalScale = hero.transform.localScale
                };
                hero.transform.localPosition = _stagedHero.HomeLocalPos + new Vector3(-3.8f, 0.25f, 0f);
                hero.transform.localScale = new Vector3(_stagedHero.HomeLocalScale.x * 0.85f, _stagedHero.HomeLocalScale.y * 0.85f, _stagedHero.HomeLocalScale.z);
            }

            // 4) Enemies start off-stage to the right (staggered)
            if (EnemySystem.Instance != null && EnemySystem.Instance.Enemies != null)
            {
                foreach (EnemyView enemy in EnemySystem.Instance.Enemies)
                {
                    if (enemy == null) continue;
                    var staged = new StagedCombatant
                    {
                        View = enemy,
                        HomeLocalPos = enemy.transform.localPosition,
                        HomeLocalScale = enemy.transform.localScale
                    };
                    enemy.transform.localPosition = staged.HomeLocalPos + new Vector3(4.2f, 0.40f, 0f);
                    enemy.transform.localScale = Vector3.zero;
                    _stagedEnemies.Add(staged);
                }
            }
        }

        private IEnumerator PlayStaggeredEntranceRoutine(Action onReadyToStartTurn)
        {
            _entranceSeq?.Kill();
            _entranceSeq = DOTween.Sequence().SetUpdate(true);

            // Step 1 (0.04s): Top Bar glides smoothly down from above
            if (_stagedTopBar != null && _stagedTopBar.Rect != null)
            {
                _entranceSeq.Insert(0.04f, _stagedTopBar.Rect.DOAnchorPos(_stagedTopBar.HomeAnchoredPos, 0.30f).SetEase(Ease.OutCubic));
                _entranceSeq.Insert(0.04f, _stagedTopBar.Group.DOFade(_stagedTopBar.OriginalAlpha, 0.24f).SetEase(Ease.OutQuad));
            }

            // Step 2 (0.10s): Hero hops onto the battlefield from the left with landing squash bounce
            if (_stagedHero != null && _stagedHero.View != null)
            {
                Transform ht = _stagedHero.View.transform;
                Vector3 homePos = _stagedHero.HomeLocalPos;
                Vector3 homeScale = _stagedHero.HomeLocalScale;
                _entranceSeq.Insert(0.10f, ht.DOLocalMoveX(homePos.x, 0.34f).SetEase(Ease.OutBack));
                _entranceSeq.Insert(0.10f, ht.DOLocalMoveY(homePos.y + 0.52f, 0.16f).SetEase(Ease.OutQuad));
                _entranceSeq.Insert(0.26f, ht.DOLocalMoveY(homePos.y, 0.16f).SetEase(Ease.InQuad));
                _entranceSeq.Insert(0.10f, ht.DOScale(homeScale, 0.24f).SetEase(Ease.OutBack));
                _entranceSeq.Insert(0.41f, ht.DOScale(new Vector3(homeScale.x * 1.12f, homeScale.y * 0.88f, homeScale.z), 0.07f).SetEase(Ease.OutQuad));
                _entranceSeq.Insert(0.48f, ht.DOScale(homeScale, 0.14f).SetEase(Ease.OutBack));
            }

            // Step 3 (0.19s + i * 0.11s): Enemies swoop in one by one from the right
            for (int i = 0; i < _stagedEnemies.Count; i++)
            {
                StagedCombatant staged = _stagedEnemies[i];
                if (staged.View == null) continue;

                Transform et = staged.View.transform;
                Vector3 homePos = staged.HomeLocalPos;
                Vector3 homeScale = staged.HomeLocalScale;
                float delay = 0.19f + i * 0.11f;

                _entranceSeq.InsertCallback(delay, () => Tune.SFX(SfxID.hover));
                _entranceSeq.Insert(delay, et.DOScale(homeScale, 0.28f).SetEase(Ease.OutBack));
                _entranceSeq.Insert(delay, et.DOLocalMoveX(homePos.x, 0.34f).SetEase(Ease.OutBack));
                _entranceSeq.Insert(delay, et.DOLocalMoveY(homePos.y + 0.45f, 0.15f).SetEase(Ease.OutQuad));
                _entranceSeq.Insert(delay + 0.15f, et.DOLocalMoveY(homePos.y, 0.16f).SetEase(Ease.InQuad));
                _entranceSeq.Insert(delay + 0.31f, et.DOScale(new Vector3(homeScale.x * 1.14f, homeScale.y * 0.86f, homeScale.z), 0.07f).SetEase(Ease.OutQuad));
                _entranceSeq.Insert(delay + 0.38f, et.DOScale(homeScale, 0.13f).SetEase(Ease.OutBack));
            }

            // Step 4 (0.32s + k * 0.06s): Bottom Combat HUD pops in left-to-right staggered
            for (int i = 0; i < _stagedUi.Count; i++)
            {
                StagedUiItem item = _stagedUi[i];
                if (item.Rect == null) continue;
                float delay = 0.32f + i * 0.06f;

                _entranceSeq.Insert(delay, item.Rect.DOAnchorPos(item.HomeAnchoredPos, 0.28f).SetEase(Ease.OutBack));
                _entranceSeq.Insert(delay, item.Rect.DOScale(item.HomeScale, 0.28f).SetEase(Ease.OutBack));
                _entranceSeq.Insert(delay, item.Group.DOFade(item.OriginalAlpha, 0.20f).SetEase(Ease.OutQuad));
            }

            // Step 5: Reveal enemy intents staggered right as the enemies finish landing!
            float intentCueTime = 0.28f + Mathf.Max(0, _stagedEnemies.Count - 1) * 0.08f + 0.16f;
            _entranceSeq.InsertCallback(intentCueTime, RevealStagedEnemyIntents);

            // Step 6: Start Combat (Turn 1 Banner + Opening Hand deal wave!)
            float turnStartTime = Mathf.Max(intentCueTime + 0.10f, 0.56f);
            bool turnStarted = false;
            _entranceSeq.InsertCallback(turnStartTime, () =>
            {
                if (turnStarted) return;
                turnStarted = true;
                _isStagingEntrance = false;
                onReadyToStartTurn?.Invoke();
            });

            _entranceSeq.OnComplete(() =>
            {
                _isStagingEntrance = false;
                _hasCompletedEntrance = true;
                if (!turnStarted)
                {
                    turnStarted = true;
                    onReadyToStartTurn?.Invoke();
                }
            });

            yield return _entranceSeq.WaitForCompletion();
        }

        private void RevealStagedEnemyIntents()
        {
            for (int i = 0; i < _stagedEnemies.Count; i++)
            {
                if (_stagedEnemies[i].View is EnemyView enemy && enemy != null && enemy.CurrentHealth > 0)
                {
                    enemy.RevealEntranceIntent(i * 0.09f);
                }
            }
        }

        private void KillAndSnapToHome()
        {
            _entranceSeq?.Kill();
            _entranceSeq = null;
            _isStagingEntrance = false;

            if (_stagedTopBar != null && _stagedTopBar.Rect != null)
            {
                _stagedTopBar.Rect.DOKill();
                _stagedTopBar.Rect.anchoredPosition = _stagedTopBar.HomeAnchoredPos;
                _stagedTopBar.Rect.localScale = _stagedTopBar.HomeScale;
                if (_stagedTopBar.Group != null) _stagedTopBar.Group.alpha = _stagedTopBar.OriginalAlpha;
            }

            for (int i = 0; i < _stagedUi.Count; i++)
            {
                StagedUiItem item = _stagedUi[i];
                if (item?.Rect == null) continue;
                item.Rect.DOKill();
                item.Rect.anchoredPosition = item.HomeAnchoredPos;
                item.Rect.localScale = item.HomeScale;
                if (item.Group != null) item.Group.alpha = item.OriginalAlpha;
            }

            if (_stagedHero?.View != null)
            {
                _stagedHero.View.transform.DOKill();
                _stagedHero.View.transform.localPosition = _stagedHero.HomeLocalPos;
                _stagedHero.View.transform.localScale = _stagedHero.HomeLocalScale;
            }

            for (int i = 0; i < _stagedEnemies.Count; i++)
            {
                StagedCombatant staged = _stagedEnemies[i];
                if (staged?.View == null) continue;
                staged.View.transform.DOKill();
                staged.View.transform.localPosition = staged.HomeLocalPos;
                staged.View.transform.localScale = staged.HomeLocalScale;
            }
        }

        private static StagedUiItem CaptureUi(RectTransform rt)
        {
            CanvasGroup cg = rt.GetComponent<CanvasGroup>();
            bool added = false;
            if (cg == null)
            {
                cg = rt.gameObject.AddComponent<CanvasGroup>();
                added = true;
            }
            return new StagedUiItem
            {
                Rect = rt,
                Group = cg,
                HomeAnchoredPos = rt.anchoredPosition,
                HomeScale = rt.localScale,
                OriginalAlpha = added ? 1f : cg.alpha,
                AddedCanvasGroup = added
            };
        }

        private static Canvas ResolveCombatCanvas()
        {
            GameObject go = GameObject.Find("GameCanvas");
            if (go != null && go.TryGetComponent(out Canvas c)) return c;
            return FindFirstObjectByType<Canvas>()?.rootCanvas;
        }
    }
}

