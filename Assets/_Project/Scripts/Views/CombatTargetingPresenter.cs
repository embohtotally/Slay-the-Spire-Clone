using System.Collections.Generic;
using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Effects;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Targeting;
using PlushieOdyssey.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.Combat.Views
{
    /// <summary>
    /// Bridges CombatHandController targeting and AoE card drags to world-space ArrowView
    /// plus Slay the Spire / Witch's Apocalyptic Journey-style animated target reticles,
    /// world Target Select rings, and live effective damage / lethal preview badges.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatTargetingPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatHandController _handController;
        [SerializeField] private CardTargetResolver _targetResolver;
        [SerializeField] private ArrowView _arrowView;
        [SerializeField] private Camera _camera;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _targetVfxPrefab;
        [SerializeField] private TMP_FontAsset _labelFont;
        [SerializeField] private float _worldPlaneZ = 0f;

        [Header("Audio")]
        [SerializeField] private SfxID _targetLockSfx = SfxID.node_hover;
        [SerializeField] private SfxID _targetBlockedSfx = SfxID.card_release_invalid;

        [Header("Indicator Styling")]
        [SerializeField] private Color _arrowIdleColor = new(0.95f, 0.96f, 1f, 0.92f);
        [SerializeField] private Color _arrowLockedColor = new(1f, 0.34f, 0.30f, 1f);
        [SerializeField] private Color _arrowAllyColor = new(0.32f, 0.82f, 1f, 1f);
        [SerializeField] private Color _arrowBlockedColor = new(1f, 0.72f, 0.28f, 0.95f);
        [SerializeField] private Color _reticleEnemyColor = new(1f, 0.35f, 0.30f, 1f);
        [SerializeField] private Color _reticleLethalColor = new(1f, 0.18f, 0.25f, 1f);
        [SerializeField] private Color _reticleAllyColor = new(0.32f, 0.85f, 1f, 1f);
        [SerializeField] private Color _reticleBlockedColor = new(1f, 0.72f, 0.28f, 1f);
        [SerializeField] private Color _reticleTauntGuideColor = new(1f, 0.86f, 0.30f, 0.95f);
        [SerializeField] private float _bracketLength = 30f;
        [SerializeField] private float _bracketThickness = 6f;
        [SerializeField] private float _pulseAmplitude = 8f;
        [SerializeField] private float _pulseSpeed = 7f;
        [SerializeField] private float _boundsPadding = 16f;

        private sealed class ActiveTargetEntry
        {
            public CombatantView Target;
            public Color Color;
            public string BadgeText;
            public bool ShowRing;
            public int PreviewDamage;
        }

        private sealed class ReticleSlot
        {
            public RectTransform Root;
            public RectTransform[] Corners; // 0:TL, 1:TR, 2:BL, 3:BR
            public readonly List<Image> AccentImages = new();
            public RectTransform BadgeRoot;
            public Image BadgeAccent;
            public TextMeshProUGUI BadgeLabel;
            public GameObject WorldRing;
        }

        private static Sprite _solidSprite;
        private RectTransform _indicatorLayer;
        private readonly List<ReticleSlot> _reticlePool = new();
        private readonly List<ActiveTargetEntry> _activeTargets = new();
        private CombatantView _lastLockedTarget;
        private CombatantView _lastBlockedTarget;
        private bool _isShowingArrow;

        private void Awake()
        {
            HideAll();
        }

        private void OnEnable()
        {
            if (_handController == null) return;
            _handController.TargetingChanged += OnTargetingChanged;
            _handController.TargetingEnded += OnTargetingEnded;
            _handController.CardDragUpdated += OnCardDragUpdated;
            _handController.CardDragEnded += OnCardDragEnded;
        }

        private void OnDisable()
        {
            if (_handController != null)
            {
                _handController.TargetingChanged -= OnTargetingChanged;
                _handController.TargetingEnded -= OnTargetingEnded;
                _handController.CardDragUpdated -= OnCardDragUpdated;
                _handController.CardDragEnded -= OnCardDragEnded;
            }
            HideAll();
        }

        private void LateUpdate()
        {
            if (_activeTargets.Count == 0) return;

            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null || _indicatorLayer == null) return;

            float pulse = Mathf.Sin(Time.unscaledTime * _pulseSpeed) * _pulseAmplitude;
            for (int i = 0; i < _activeTargets.Count; i++)
            {
                ActiveTargetEntry entry = _activeTargets[i];
                if (i >= _reticlePool.Count) break;
                ReticleSlot slot = _reticlePool[i];

                if (entry.Target == null || !entry.Target.isActiveAndEnabled || entry.Target.CurrentHealth <= 0)
                {
                    SetSlotVisible(slot, false);
                    continue;
                }

                PositionReticle(slot, entry, cam, pulse);
                if (entry.PreviewDamage > 0)
                {
                    entry.Target.ShowDamagePreview(entry.PreviewDamage);
                }
            }
        }

        private void OnTargetingChanged(CardView cardView, Vector2 pointerScreenPosition)
        {
            if (cardView == null) return;

            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;

            Card card = cardView.Card;
            CombatantView hovered = null;
            bool validLock = false;
            bool blockedByTaunt = false;

            if (_targetResolver != null && card != null &&
                _targetResolver.TryRaycastCombatant(pointerScreenPosition, out CombatantView candidate))
            {
                hovered = candidate;
                validLock = _targetResolver.IsValidTarget(card, candidate);
                blockedByTaunt = !validLock && _targetResolver.IsBlockedByTaunt(card, candidate);
            }

            UpdateArrow(cardView, pointerScreenPosition, cam, hovered, validLock, blockedByTaunt, card);
            UpdateTargetAudio(hovered, validLock, blockedByTaunt);
            UpdateManualTargetReticles(card, hovered, validLock, blockedByTaunt);
        }

        private void UpdateTargetAudio(CombatantView hovered, bool validLock, bool blockedByTaunt)
        {
            if (validLock && hovered != null)
            {
                if (_lastLockedTarget != hovered)
                {
                    _lastLockedTarget = hovered;
                    _lastBlockedTarget = null;
                    if (_targetLockSfx != SfxID.None) Tune.SFX(_targetLockSfx);
                }
                return;
            }

            _lastLockedTarget = null;
            if (blockedByTaunt && hovered != null)
            {
                if (_lastBlockedTarget != hovered)
                {
                    _lastBlockedTarget = hovered;
                    if (_targetBlockedSfx != SfxID.None) Tune.SFX(_targetBlockedSfx);
                }
                return;
            }

            _lastBlockedTarget = null;
        }

        private void OnTargetingEnded()
        {
            HideAll();
        }

        private void OnCardDragUpdated(CardView cardView, Vector2 pointerScreenPosition, bool inPlayRegion)
        {
            if (cardView == null || cardView.Card == null || !inPlayRegion)
            {
                ClearReticles();
                return;
            }

            Card card = cardView.Card;
            if (card.OtherEffects == null || card.OtherEffects.Count == 0)
            {
                ClearReticles();
                return;
            }

            HeroView caster = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            List<CombatantView> affectedEnemies = new();
            foreach (AutoTargetEffect autoEffect in card.OtherEffects)
            {
                if (autoEffect == null || autoEffect.TargetMode == null || autoEffect.Effect == null) continue;
                List<CombatantView> targets = autoEffect.TargetMode.GetTargets(caster);
                if (targets == null) continue;
                foreach (CombatantView t in targets)
                {
                    if (t is EnemyView && t.isActiveAndEnabled && t.CurrentHealth > 0 && !affectedEnemies.Contains(t))
                    {
                        affectedEnemies.Add(t);
                    }
                }
            }

            if (affectedEnemies.Count == 0)
            {
                ClearReticles();
                return;
            }

            _activeTargets.Clear();
            foreach (CombatantView enemy in affectedEnemies)
            {
                BuildEnemyPreviewEntry(card, enemy, caster, isManual: false);
            }
            ApplyActiveTargets();
        }

        private void OnCardDragEnded()
        {
            ClearReticles();
        }

        private void UpdateArrow(
            CardView cardView,
            Vector2 pointerScreenPosition,
            Camera cam,
            CombatantView hovered,
            bool validLock,
            bool blockedByTaunt,
            Card card)
        {
            if (_arrowView == null) return;

            Vector2 originScreen = GetCardOriginScreenPoint(cardView, pointerScreenPosition);
            if (!TryScreenToWorldPlane(cam, originScreen, _worldPlaneZ, out Vector3 worldStart))
                return;

            if (!_isShowingArrow || !_arrowView.gameObject.activeSelf)
            {
                _arrowView.gameObject.SetActive(true);
                _arrowView.SetupArrow(worldStart);
                _isShowingArrow = true;
            }
            else
            {
                _arrowView.SetStartPosition(worldStart);
            }

            if (validLock && hovered != null)
            {
                _arrowView.SetTargetTransform(hovered.transform);
                bool isAlly = hovered is HeroView || (card != null && card.TargetType == CardTargetType.Hero);
                _arrowView.SetTint(isAlly ? _arrowAllyColor : _arrowLockedColor);
            }
            else
            {
                if (TryScreenToWorldPlane(cam, pointerScreenPosition, _worldPlaneZ, out Vector3 worldEnd))
                    _arrowView.SetEndPositionOverride(worldEnd);
                else
                    _arrowView.SetEndPositionOverride(null);

                _arrowView.SetTint(blockedByTaunt ? _arrowBlockedColor : _arrowIdleColor);
            }
        }

        private void UpdateManualTargetReticles(Card card, CombatantView hovered, bool validLock, bool blockedByTaunt)
        {
            _activeTargets.Clear();
            HeroView caster = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;

            if (validLock && hovered != null && card != null)
            {
                List<CombatantView> resolvedTargets = ResolveManualPreviewTargets(card, hovered, caster);
                foreach (CombatantView target in resolvedTargets)
                {
                    if (target is EnemyView)
                    {
                        BuildEnemyPreviewEntry(card, target, caster, isManual: true);
                    }
                    else
                    {
                        BuildAllyPreviewEntry(card, target, caster, isManual: true);
                    }
                }
            }
            else if (blockedByTaunt && hovered != null)
            {
                _activeTargets.Add(new ActiveTargetEntry
                {
                    Target = hovered,
                    Color = _reticleBlockedColor,
                    BadgeText = $"<b>{GetCombatantDisplayName(hovered)}</b>  <color=#FFB74D>BLOCKED BY TAUNT</color>",
                    ShowRing = false
                });

                if (EnemySystem.Instance != null && EnemySystem.Instance.Enemies != null)
                {
                    foreach (EnemyView enemy in EnemySystem.Instance.Enemies)
                    {
                        if (enemy != null && enemy.isActiveAndEnabled && enemy.CurrentHealth > 0 && enemy.IsTaunted)
                        {
                            _activeTargets.Add(new ActiveTargetEntry
                            {
                                Target = enemy,
                                Color = _reticleTauntGuideColor,
                                BadgeText = $"<b>{GetCombatantDisplayName(enemy)}</b>  <color=#FFE066>MUST TARGET (TAUNT)</color>",
                                ShowRing = true
                            });
                        }
                    }
                }
            }

            ApplyActiveTargets();
        }

        private void BuildAllyPreviewEntry(Card card, CombatantView target, HeroView caster, bool isManual)
        {
            string allyName = GetCombatantDisplayName(target);
            int availableMana = ManaSystem.Instance != null ? ManaSystem.Instance.AvailableMana : 0;
            int effectiveX = card != null ? card.GetEffectiveX(availableMana) : 1;

            string badge;
            Color color = _reticleAllyColor;
            if (card != null && card.IsXCost && effectiveX <= 0)
            {
                badge = $"<b>{allyName}</b>   <color=#FFB74D>0 EFFECT (0 MANA)</color>";
                color = _reticleBlockedColor;
            }
            else
            {
                string summary = BuildNonDamageEffectSummary(card, target, caster, isManual, effectiveX);
                badge = !string.IsNullOrEmpty(summary)
                    ? $"<b>{allyName}</b>   <color=#64E8FF>{summary}</color>"
                    : $"<b>{allyName}</b>   <color=#64E8FF>ALLY</color>";
            }

            _activeTargets.Add(new ActiveTargetEntry
            {
                Target = target,
                Color = color,
                BadgeText = badge,
                ShowRing = true
            });
        }

        private void BuildEnemyPreviewEntry(Card card, CombatantView target, HeroView caster, bool isManual)
        {
            int expectedDamage = EstimateCardDamage(card, target, caster, isManual);
            string enemyName = GetCombatantDisplayName(target);
            bool hasDamage = expectedDamage > 0;
            bool isLethal = hasDamage && expectedDamage >= (target.CurrentHealth + target.CurrentShield);
            int availableMana = ManaSystem.Instance != null ? ManaSystem.Instance.AvailableMana : 0;
            int effectiveX = card != null ? card.GetEffectiveX(availableMana) : 1;

            string badge;
            Color color;
            if (isLethal)
            {
                badge = $"<b>{enemyName}</b>   <color=#FF4D5A>{expectedDamage} DMG  [LETHAL]</color>";
                color = _reticleLethalColor;
            }
            else if (hasDamage)
            {
                badge = $"<b>{enemyName}</b>   <color=#FF766B>{expectedDamage} DMG</color>";
                color = _reticleEnemyColor;
            }
            else if (card != null && card.IsXCost && effectiveX <= 0)
            {
                badge = $"<b>{enemyName}</b>   <color=#FFB74D>0 EFFECT (0 MANA)</color>";
                color = _reticleBlockedColor;
            }
            else
            {
                string summary = BuildNonDamageEffectSummary(card, target, caster, isManual, effectiveX);
                badge = !string.IsNullOrEmpty(summary)
                    ? $"<b>{enemyName}</b>   <color=#A8F0A5>{summary}</color>"
                    : $"<b>{enemyName}</b>   <color=#FFE082>TARGET</color>";
                color = _reticleEnemyColor;
            }

            _activeTargets.Add(new ActiveTargetEntry
            {
                Target = target,
                Color = color,
                BadgeText = badge,
                ShowRing = true,
                PreviewDamage = expectedDamage
            });
        }

        private static string BuildNonDamageEffectSummary(Card card, CombatantView target, CombatantView caster, bool includeManual, int xMultiplier)
        {
            if (card == null || xMultiplier <= 0) return string.Empty;

            List<string> parts = new();
            void Collect(Effect effect)
            {
                if (effect == null) return;
                switch (effect)
                {
                    case HealEffect heal when heal.HealAmount > 0:
                        parts.Add($"+{heal.HealAmount * xMultiplier} HP");
                        break;
                    case GainShieldEffect shield when shield.ShieldAmount > 0:
                        parts.Add($"+{shield.ShieldAmount * xMultiplier} SHIELD");
                        break;
                    case ApplyStatusEffect status:
                        status.ResolveScaled(xMultiplier, out int sStacks, out int sDur);
                        if (status.Status == StatusType.Poison && sStacks > 0)
                            parts.Add($"+{sStacks} POISON");
                        else if (sDur > 0)
                            parts.Add($"{status.Status.ToString().ToUpper()} {sDur}T");
                        break;
                    case ApplyBuffEffect buff:
                        buff.ResolveScaled(xMultiplier, out int bVal, out int bDur);
                        if (buff.BuffType == BuffType.Poison && bVal > 0)
                            parts.Add($"+{bVal} POISON");
                        else if (buff.BuffType == BuffType.Weak || buff.BuffType == BuffType.Vulnerable ||
                                 buff.BuffType == BuffType.Frail || buff.BuffType == BuffType.Intangible ||
                                 buff.BuffType == BuffType.FearImmunity || buff.BuffType == BuffType.Redirect)
                            parts.Add($"{buff.BuffType.ToString().ToUpper()} {bDur}T");
                        else if (bVal > 0)
                            parts.Add($"+{bVal} {buff.BuffType.ToString().ToUpper()}");
                        break;
                    case ApplyDoTEffect dot:
                        dot.ResolveScaled(xMultiplier, out int dpt, out int dDur);
                        if (dpt > 0) parts.Add($"{dpt} DOT/{dDur}T");
                        break;
                    case ApplyStunEffect stun when stun.Duration > 0:
                        parts.Add($"STUN {stun.Duration * xMultiplier}T");
                        break;
                    case ApplyTauntEffect taunt when taunt.Duration > 0:
                        parts.Add($"TAUNT {taunt.Duration * xMultiplier}T");
                        break;
                }
            }

            if (includeManual && card.ManualTargetEffect != null)
            {
                Collect(card.ManualTargetEffect);
            }

            if (card.OtherEffects != null)
            {
                foreach (AutoTargetEffect auto in card.OtherEffects)
                {
                    if (auto == null || auto.Effect == null || auto.TargetMode == null) continue;
                    List<CombatantView> autoTargets = auto.TargetMode.GetTargets(caster);
                    if (autoTargets != null && autoTargets.Contains(target))
                    {
                        Collect(auto.Effect);
                    }
                }
            }

            return parts.Count > 0 ? string.Join("  ", parts) : string.Empty;
        }

        private static List<CombatantView> ResolveManualPreviewTargets(Card card, CombatantView primaryTarget, CombatantView caster)
        {
            List<CombatantView> targets = new() { primaryTarget };

            if (card.ManualTargetEffect is BeamAttackEffect beam &&
                beam.BeamConfig != null && beam.BeamConfig.ExpandSingleTargetToLine && caster != null)
            {
                List<CombatantView> expanded = BeamTargetQuery.ExpandToLine(
                    caster, primaryTarget, beam.BeamConfig.LineMaxAngle, beam.BeamConfig.LineMaxTargets);
                if (expanded != null && expanded.Count > 0)
                {
                    return expanded;
                }
            }

            return targets;
        }

        private static int EstimateCardDamage(Card card, CombatantView target, CombatantView caster, bool includeManual)
        {
            if (card == null || target == null) return 0;

            int availableMana = ManaSystem.Instance != null ? ManaSystem.Instance.AvailableMana : 0;
            int xMultiplier = card.GetEffectiveX(availableMana);
            if (xMultiplier <= 0) return 0;

            int total = 0;

            if (includeManual && card.ManualTargetEffect != null)
            {
                total += EstimateEffectDamage(card.ManualTargetEffect, target, caster, xMultiplier, card);
            }

            if (card.OtherEffects != null)
            {
                foreach (AutoTargetEffect auto in card.OtherEffects)
                {
                    if (auto == null || auto.Effect == null || auto.TargetMode == null) continue;
                    List<CombatantView> autoTargets = auto.TargetMode.GetTargets(caster);
                    if (autoTargets != null && autoTargets.Contains(target))
                    {
                        total += EstimateEffectDamage(auto.Effect, target, caster, xMultiplier, card);
                    }
                }
            }

            return total;
        }

        private static int EstimateEffectDamage(Effect effect, CombatantView target, CombatantView caster, int xMultiplier = 1, Card sourceCard = null)
        {
            if (xMultiplier <= 0) return 0;

            int bonus = sourceCard != null ? sourceCard.CombatUpgradeBonus : 0;

            if (effect is MultiHitDamageEffect multi)
            {
                int hits = multi.ResolveHitCount(xMultiplier);
                int perHitRaw = multi.ResolveDamagePerHit(xMultiplier) + bonus;
                if (hits <= 0 || perHitRaw <= 0) return 0;
                int perHitCalc = BuffSystem.Instance != null
                    ? BuffSystem.Instance.CalculateExpectedDamage(perHitRaw, caster, target)
                    : perHitRaw;
                return perHitCalc * hits;
            }

            int raw = effect switch
            {
                DealDamageEffect dmg => (dmg.DamageAmount + bonus) * xMultiplier,
                BeamAttackEffect beam => (beam.DamageAmount + bonus) * xMultiplier,
                ConditionalDamageEffect cond => (cond.EvaluateDamage(new List<CombatantView> { target }, caster) + bonus) * xMultiplier,
                ComboDamageEffect combo => (combo.EvaluateDamage(sourceCard) + bonus) * xMultiplier,
                _ => 0
            };

            if (raw <= 0) return 0;
            return BuffSystem.Instance != null
                ? BuffSystem.Instance.CalculateExpectedDamage(raw, caster, target)
                : raw;
        }

        private static string GetCombatantDisplayName(CombatantView combatant)
        {
            if (combatant is EnemyView enemy && enemy.Data != null && !string.IsNullOrWhiteSpace(enemy.Data.name))
                return enemy.Data.name;
            return combatant != null ? combatant.name.Replace("(Clone)", "").Trim() : "Target";
        }

        private void ApplyActiveTargets()
        {
            if (_activeTargets.Count == 0)
            {
                ClearReticles();
                return;
            }

            EnsureIndicatorLayer();
            EnsurePoolSize(_activeTargets.Count);

            Camera cam = _camera != null ? _camera : Camera.main;
            float pulse = Mathf.Sin(Time.unscaledTime * _pulseSpeed) * _pulseAmplitude;

            for (int i = 0; i < _reticlePool.Count; i++)
            {
                ReticleSlot slot = _reticlePool[i];
                if (i < _activeTargets.Count)
                {
                    ActiveTargetEntry entry = _activeTargets[i];
                    SetSlotVisible(slot, true);
                    ApplySlotStyle(slot, entry);
                    if (cam != null) PositionReticle(slot, entry, cam, pulse);
                }
                else
                {
                    SetSlotVisible(slot, false);
                }
            }
        }

        private void ApplySlotStyle(ReticleSlot slot, ActiveTargetEntry entry)
        {
            foreach (Image img in slot.AccentImages)
            {
                if (img != null) img.color = entry.Color;
            }
            if (slot.BadgeAccent != null) slot.BadgeAccent.color = entry.Color;
            if (slot.BadgeLabel != null) slot.BadgeLabel.text = entry.BadgeText;
            if (slot.WorldRing != null) slot.WorldRing.SetActive(entry.ShowRing);
        }

        private void PositionReticle(ReticleSlot slot, ActiveTargetEntry entry, Camera cam, float pulse)
        {
            Bounds worldBounds = GetCombatantWorldBounds(entry.Target);
            Vector3 center = worldBounds.center;
            Vector3 ext = worldBounds.extents;

            Vector2 minScreen = new(float.MaxValue, float.MaxValue);
            Vector2 maxScreen = new(float.MinValue, float.MinValue);

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector3 cornerWorld = new(center.x + sx * ext.x, center.y + sy * ext.y, center.z);
                    Vector3 sp = cam.WorldToScreenPoint(cornerWorld);
                    minScreen.x = Mathf.Min(minScreen.x, sp.x);
                    minScreen.y = Mathf.Min(minScreen.y, sp.y);
                    maxScreen.x = Mathf.Max(maxScreen.x, sp.x);
                    maxScreen.y = Mathf.Max(maxScreen.y, sp.y);
                }
            }

            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_indicatorLayer, minScreen, uiCamera, out Vector2 minLocal) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(_indicatorLayer, maxScreen, uiCamera, out Vector2 maxLocal))
                return;

            Vector2 boxCenter = (minLocal + maxLocal) * 0.5f;
            float width = Mathf.Clamp(Mathf.Abs(maxLocal.x - minLocal.x) + _boundsPadding * 2f, 150f, 440f);
            float height = Mathf.Clamp(Mathf.Abs(maxLocal.y - minLocal.y) + _boundsPadding * 2f, 150f, 420f);

            slot.Root.anchoredPosition = boxCenter;
            slot.Root.sizeDelta = new Vector2(width + pulse * 2f, height + pulse * 2f);

            if (slot.BadgeRoot != null)
            {
                slot.BadgeRoot.anchoredPosition = new Vector2(0f, (height + pulse * 2f) * 0.5f + 56f);
            }

            if (slot.WorldRing != null && entry.ShowRing)
            {
                slot.WorldRing.transform.position = new Vector3(center.x, center.y, _worldPlaneZ + 0.05f);
            }
        }

        private static Bounds GetCombatantWorldBounds(CombatantView target)
        {
            if (target.SpriteRenderer != null && target.SpriteRenderer.sprite != null)
            {
                Bounds b = target.SpriteRenderer.bounds;
                if (b.size.sqrMagnitude > 0.05f) return b;
            }

            Collider col = target.GetComponentInChildren<Collider>();
            if (col != null && col.bounds.size.sqrMagnitude > 0.05f)
            {
                return col.bounds;
            }

            return new Bounds(target.transform.position, new Vector3(1.8f, 2.2f, 0.5f));
        }

        private void EnsureIndicatorLayer()
        {
            if (_indicatorLayer != null) return;
            if (_canvas == null) _canvas = FindFirstObjectByType<Canvas>();
            if (_canvas == null) return;

            Transform existing = _canvas.transform.Find("CombatTargetIndicators");
            if (existing is RectTransform existingRect)
            {
                _indicatorLayer = existingRect;
            }
            else
            {
                GameObject layerGo = new("CombatTargetIndicators", typeof(RectTransform), typeof(CanvasGroup));
                layerGo.layer = _canvas.gameObject.layer;
                _indicatorLayer = (RectTransform)layerGo.transform;
                _indicatorLayer.SetParent(_canvas.transform, false);
                _indicatorLayer.anchorMin = Vector2.zero;
                _indicatorLayer.anchorMax = Vector2.one;
                _indicatorLayer.offsetMin = Vector2.zero;
                _indicatorLayer.offsetMax = Vector2.zero;
                _indicatorLayer.SetAsFirstSibling();

                CanvasGroup group = layerGo.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            Transform playArea = _canvas.transform.Find("PlayArea");
            if (playArea != null)
            {
                _indicatorLayer.SetSiblingIndex(playArea.GetSiblingIndex() + 1);
            }
            else
            {
                _indicatorLayer.SetAsFirstSibling();
            }
        }

        private void EnsurePoolSize(int count)
        {
            while (_reticlePool.Count < count)
            {
                _reticlePool.Add(CreateReticleSlot(_reticlePool.Count));
            }
        }

        private ReticleSlot CreateReticleSlot(int index)
        {
            Sprite solid = GetSolidSprite();
            GameObject rootGo = new($"TargetReticle_{index}", typeof(RectTransform));
            rootGo.layer = _indicatorLayer.gameObject.layer;
            RectTransform root = (RectTransform)rootGo.transform;
            root.SetParent(_indicatorLayer, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            var slot = new ReticleSlot
            {
                Root = root,
                Corners = new RectTransform[4]
            };

            // 4 L-shaped corners: TL, TR, BL, BR
            Vector2[] anchors = { new(0f, 1f), new(1f, 1f), new(0f, 0f), new(1f, 0f) };
            float[] dirX = { 1f, -1f, 1f, -1f };
            float[] dirY = { -1f, -1f, 1f, 1f };

            for (int c = 0; c < 4; c++)
            {
                GameObject cornerGo = new($"Corner_{c}", typeof(RectTransform));
                cornerGo.layer = rootGo.layer;
                RectTransform corner = (RectTransform)cornerGo.transform;
                corner.SetParent(root, false);
                corner.anchorMin = corner.anchorMax = anchors[c];
                corner.pivot = anchors[c];
                corner.anchoredPosition = Vector2.zero;
                corner.sizeDelta = new Vector2(_bracketLength, _bracketLength);
                slot.Corners[c] = corner;

                CreateBracketBar(corner, "ShadowH", solid, new Color(0f, 0f, 0f, 0.72f),
                    anchors[c], new Vector2(dirX[c] * -2f, dirY[c] * -2f),
                    new Vector2(_bracketLength + 4f, _bracketThickness + 4f), null);
                CreateBracketBar(corner, "ShadowV", solid, new Color(0f, 0f, 0f, 0.72f),
                    anchors[c], new Vector2(dirX[c] * -2f, dirY[c] * -2f),
                    new Vector2(_bracketThickness + 4f, _bracketLength + 4f), null);

                CreateBracketBar(corner, "ArmH", solid, _reticleEnemyColor,
                    anchors[c], Vector2.zero,
                    new Vector2(_bracketLength, _bracketThickness), slot.AccentImages);
                CreateBracketBar(corner, "ArmV", solid, _reticleEnemyColor,
                    anchors[c], Vector2.zero,
                    new Vector2(_bracketThickness, _bracketLength), slot.AccentImages);
            }

            // Floating target badge pill
            GameObject badgeGo = new("TargetBadge", typeof(RectTransform), typeof(Image));
            badgeGo.layer = rootGo.layer;
            RectTransform badgeRect = (RectTransform)badgeGo.transform;
            badgeRect.SetParent(root, false);
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(270f, 38f);
            Image badgeBg = badgeGo.GetComponent<Image>();
            badgeBg.sprite = solid;
            badgeBg.color = new Color(0.06f, 0.07f, 0.11f, 0.90f);
            badgeBg.raycastTarget = false;

            GameObject accentGo = new("TopAccent", typeof(RectTransform), typeof(Image));
            accentGo.layer = rootGo.layer;
            RectTransform accentRect = (RectTransform)accentGo.transform;
            accentRect.SetParent(badgeRect, false);
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(0f, 4f);
            accentRect.anchoredPosition = Vector2.zero;
            Image accentImg = accentGo.GetComponent<Image>();
            accentImg.sprite = solid;
            accentImg.color = _reticleEnemyColor;
            accentImg.raycastTarget = false;

            GameObject labelGo = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.layer = rootGo.layer;
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(badgeRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 2f);
            labelRect.offsetMax = new Vector2(-10f, -4f);
            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            label.fontSize = 19f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            if (_labelFont != null) label.font = _labelFont;
            else if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;

            slot.BadgeRoot = badgeRect;
            slot.BadgeAccent = accentImg;
            slot.BadgeLabel = label;

            if (_targetVfxPrefab != null)
            {
                slot.WorldRing = Instantiate(_targetVfxPrefab, transform);
                slot.WorldRing.name = $"TargetRing_{index}";
                slot.WorldRing.SetActive(false);
            }

            SetSlotVisible(slot, false);
            return slot;
        }

        private static void CreateBracketBar(
            RectTransform parent,
            string barName,
            Sprite sprite,
            Color color,
            Vector2 anchorPivot,
            Vector2 offset,
            Vector2 size,
            List<Image> trackAccent)
        {
            GameObject go = new(barName, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchorPivot;
            rect.pivot = anchorPivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            trackAccent?.Add(img);
        }

        private static void SetSlotVisible(ReticleSlot slot, bool visible)
        {
            if (slot.Root != null && slot.Root.gameObject.activeSelf != visible)
                slot.Root.gameObject.SetActive(visible);
            if (slot.WorldRing != null && !visible && slot.WorldRing.activeSelf)
                slot.WorldRing.SetActive(false);
        }

        private void ClearReticles()
        {
            for (int i = 0; i < _activeTargets.Count; i++)
            {
                if (_activeTargets[i]?.Target != null)
                {
                    _activeTargets[i].Target.ClearDamagePreview();
                }
            }
            if (EnemySystem.Instance != null && EnemySystem.Instance.Enemies != null)
            {
                foreach (EnemyView e in EnemySystem.Instance.Enemies)
                {
                    if (e != null) e.ClearDamagePreview();
                }
            }
            _activeTargets.Clear();
            foreach (ReticleSlot slot in _reticlePool)
            {
                SetSlotVisible(slot, false);
            }
        }

        private void HideAll()
        {
            _isShowingArrow = false;
            _lastLockedTarget = null;
            _lastBlockedTarget = null;
            if (_arrowView != null)
            {
                _arrowView.SetTargetTransform(null);
                _arrowView.SetEndPositionOverride(null);
                _arrowView.SetTint(_arrowIdleColor);
                if (_arrowView.gameObject.activeSelf) _arrowView.gameObject.SetActive(false);
            }
            ClearReticles();
        }

        private Vector2 GetCardOriginScreenPoint(CardView cardView, Vector2 fallback)
        {
            if (cardView.transform is not RectTransform cardRect)
                return fallback;

            RectTransform visualRect = cardRect.Find("Wrapper") as RectTransform ?? cardRect;
            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;
            Vector3 topCenter = visualRect.TransformPoint(new Vector3(visualRect.rect.center.x, visualRect.rect.yMax, 0f));
            return RectTransformUtility.WorldToScreenPoint(uiCamera, topCenter);
        }

        private static bool TryScreenToWorldPlane(Camera cam, Vector2 screenPoint, float zPlane, out Vector3 worldPoint)
        {
            Plane plane = new(cam.transform.forward, new Vector3(0f, 0f, zPlane));
            Ray ray = cam.ScreenPointToRay(screenPoint);
            if (plane.Raycast(ray, out float distance))
            {
                worldPoint = ray.GetPoint(distance);
                return true;
            }

            worldPoint = Vector3.zero;
            return false;
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            Texture2D tex = Texture2D.whiteTexture;
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _solidSprite;
        }
    }
}
