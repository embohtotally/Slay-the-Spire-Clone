using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.States;
using PlushieOdyssey.Core;
using PlushieOdyssey.Core.StateMachines;
using PlushieOdyssey.Vfx;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlushieOdyssey.Combat.Views
{
    public class CombatantView : MonoBehaviour
    {
        [Header("Health Visual")]
        [SerializeField] private bool useSlider;
        [SerializeField, HideIf("useSlider")] private TMP_Text healthText;
        [SerializeField, ShowIf("useSlider")] private Slider healthSlider;
        [SerializeField, ShowIf("useSlider")] private Slider blockedSlider;
        [SerializeField, ShowIf("useSlider")] private Slider shieldSlider;

        [Header("Others")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] private List<SpriteRenderer> additionalSpriteRenderers = new();
        [SerializeField] private Animator animator;
        [SerializeField] private List<Animator> additionalAnimators = new();
        [SerializeField] private string hurtAnimationTrigger = "Hurt";
        [SerializeField] private string deadAnimationTrigger = "Die";
        [SerializeField] private float shakeDuration;
        [SerializeField] private float shakeStrength;
        [SerializeField] private global::PlushieOdyssey.Audio.SfxID hitSfx = global::PlushieOdyssey.Audio.SfxID.Hit;
        [SerializeField] private global::PlushieOdyssey.Audio.SfxID healSfx = global::PlushieOdyssey.Audio.SfxID.Heal;
        [SerializeField] private global::PlushieOdyssey.Audio.SfxID shieldSfx = global::PlushieOdyssey.Audio.SfxID.Shield;

        public Animator Animator => animator;
        public List<Animator> AdditionalAnimators => additionalAnimators;
        public string HurtAnimationTrigger => hurtAnimationTrigger;
        public string DeadAnimationTrigger => deadAnimationTrigger;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public List<SpriteRenderer> AdditionalSpriteRenderers => additionalSpriteRenderers;

        public bool HasAnimator => animator != null || (additionalAnimators != null && additionalAnimators.Count > 0);

        public int MaxHealth { get; private set; }
        public int CurrentHealth { get; private set; }
        public int HealthPenalty { get; protected set; }
        public int EffectiveMaxHealth => Mathf.Max(1, MaxHealth - HealthPenalty);

        public int StunDuration { get; private set; }
        public bool IsStunned => StunDuration > 0;

        public int CurrentShield { get; private set; }

        public int TauntDuration { get; private set; }
        public bool IsTaunted => TauntDuration > 0;

        public float ShakeDuration => shakeDuration;
        public float ShakeStrength => shakeStrength;

        private bool hasHealthVisual;
        private Canvas combatantCanvas;

        // === Procedural Juice & Live Damage Preview ===
        private Transform _visualRoot;
        private Vector3 _visualHomeLocalPos;
        private Vector3 _visualHomeLocalScale = Vector3.one;
        private bool _visualHomeCaptured;
        private float _idlePhaseOffset;
        private float _idleElapsed;
        private bool _isPlayingActionMotion;
        private Sequence _proceduralMotionSeq;
        private Image _damagePreviewFill;
        private int _previewIncomingDamage;
        private static Sprite s_previewSolidSprite;

        // === Frieren Shield VFX (auto-attached by FrierenShieldSystem) ===
        private FrierenShieldController shieldVfx;

        private void NotifyShieldDeployed(int amount)
        {
            if (shieldVfx == null && FrierenShieldSystem.Instance != null)
                shieldVfx = FrierenShieldSystem.Instance.Attach(this);
            if (shieldVfx != null) shieldVfx.Deploy(amount);
        }

        private void NotifyShieldHit(int absorbed)
        {
            if (shieldVfx == null) return;
            shieldVfx.OnHit(absorbed, FrierenShieldSystem.ResolveHitPosition(this));
        }

        private void NotifyShieldBroken()
        {
            if (shieldVfx != null) shieldVfx.Shatter();
        }

        private void NotifyShieldCleared()
        {
            if (shieldVfx != null) shieldVfx.Dissolve();
        }

        public StateMachine StateMachine { get; private set; }

        public event Action StatusEffectsChanged;
        public event Action HealthChanged;

        protected void SetupBase(int maxHealth, Sprite image)
        {
            MaxHealth = CurrentHealth = maxHealth;
            spriteRenderer.sprite = image;

            StateMachine = new StateMachine();
            StateMachine.Initialize(new CombatantIdleState(this));

            SetupHealthVisual();
            UpdateHealthVisual(true);
            _idlePhaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            CaptureProceduralVisualHomeIfNeeded(forceRecapture: true);
        }

        public void CaptureProceduralVisualHomeIfNeeded(bool forceRecapture = false)
        {
            Transform candidate = spriteRenderer != null ? spriteRenderer.transform : transform;
            if (!forceRecapture && _visualHomeCaptured && _visualRoot == candidate) return;

            _visualRoot = candidate;
            _visualHomeLocalPos = _visualRoot.localPosition;
            _visualHomeLocalScale = _visualRoot.localScale;
            if (_visualHomeLocalScale.sqrMagnitude < 0.0001f) _visualHomeLocalScale = Vector3.one;
            _visualHomeCaptured = true;
        }

        public void UpdateProceduralIdle(float dt)
        {
            if (_isPlayingActionMotion || CurrentHealth <= 0) return;
            CaptureProceduralVisualHomeIfNeeded();
            if (_visualRoot == null) return;

            _idleElapsed += dt;
            float wave = Mathf.Sin(_idleElapsed * 2.5f + _idlePhaseOffset);
            float breatheX = 1f + wave * 0.024f;
            float breatheY = 1f - wave * 0.026f;
            float bobY = Mathf.Sin(_idleElapsed * 2.5f + _idlePhaseOffset - 0.5f) * 0.045f;

            _visualRoot.localPosition = _visualHomeLocalPos + new Vector3(0f, bobY, 0f);
            _visualRoot.localScale = new Vector3(
                _visualHomeLocalScale.x * breatheX,
                _visualHomeLocalScale.y * breatheY,
                _visualHomeLocalScale.z);
        }

        public void PlayProceduralAttackLunge(Vector3 targetWorldPos, float duration = 0.34f)
        {
            CaptureProceduralVisualHomeIfNeeded();
            if (_visualRoot == null) return;

            _proceduralMotionSeq?.Kill();
            _isPlayingActionMotion = true;

            Vector3 worldDir = targetWorldPos - transform.position;
            worldDir.z = 0f;
            float sign = worldDir.x >= 0f ? 1f : -1f;
            Vector3 localForward = transform.InverseTransformDirection(new Vector3(sign, 0f, 0f));
            if (localForward.sqrMagnitude < 0.01f) localForward = Vector3.right * sign;
            localForward.Normalize();

            float lungeDist = 0.72f;
            float windupDist = -0.22f;
            float windupT = duration * 0.24f;
            float dashT = duration * 0.28f;
            float returnT = duration * 0.48f;

            _proceduralMotionSeq = DOTween.Sequence().SetUpdate(true);
            _proceduralMotionSeq.Append(_visualRoot.DOLocalMove(_visualHomeLocalPos + localForward * windupDist, windupT).SetEase(Ease.InQuad));
            _proceduralMotionSeq.Join(_visualRoot.DOScale(new Vector3(_visualHomeLocalScale.x * 1.12f, _visualHomeLocalScale.y * 0.88f, _visualHomeLocalScale.z), windupT));
            _proceduralMotionSeq.Append(_visualRoot.DOLocalMove(_visualHomeLocalPos + localForward * lungeDist, dashT).SetEase(Ease.OutExpo));
            _proceduralMotionSeq.Join(_visualRoot.DOScale(new Vector3(_visualHomeLocalScale.x * 0.90f, _visualHomeLocalScale.y * 1.12f, _visualHomeLocalScale.z), dashT));
            _proceduralMotionSeq.Append(_visualRoot.DOLocalMove(_visualHomeLocalPos, returnT).SetEase(Ease.OutBack));
            _proceduralMotionSeq.Join(_visualRoot.DOScale(_visualHomeLocalScale, returnT).SetEase(Ease.OutBack));
            _proceduralMotionSeq.OnComplete(() => _isPlayingActionMotion = false);
        }

        public void PlayProceduralSupportPulse()
        {
            CaptureProceduralVisualHomeIfNeeded();
            if (_visualRoot == null) return;

            _proceduralMotionSeq?.Kill();
            _isPlayingActionMotion = true;

            _proceduralMotionSeq = DOTween.Sequence().SetUpdate(true);
            _proceduralMotionSeq.Append(_visualRoot.DOScale(new Vector3(_visualHomeLocalScale.x * 0.92f, _visualHomeLocalScale.y * 1.12f, _visualHomeLocalScale.z), 0.12f).SetEase(Ease.OutQuad));
            _proceduralMotionSeq.Join(_visualRoot.DOLocalMoveY(_visualHomeLocalPos.y + 0.22f, 0.12f).SetEase(Ease.OutQuad));
            _proceduralMotionSeq.Append(_visualRoot.DOScale(_visualHomeLocalScale, 0.18f).SetEase(Ease.OutBack));
            _proceduralMotionSeq.Join(_visualRoot.DOLocalMove(_visualHomeLocalPos, 0.18f).SetEase(Ease.OutCubic));
            _proceduralMotionSeq.OnComplete(() => _isPlayingActionMotion = false);
        }

        public void PlayProceduralHurtRecoil(float duration = 0.22f, float strength = 0.24f)
        {
            CaptureProceduralVisualHomeIfNeeded();
            if (_visualRoot == null) return;

            _proceduralMotionSeq?.Kill();
            _isPlayingActionMotion = true;

            float knockSign = this is EnemyView ? 1f : -1f;
            Vector3 localBack = transform.InverseTransformDirection(new Vector3(knockSign, 0f, 0f));
            if (localBack.sqrMagnitude < 0.01f) localBack = Vector3.right * knockSign;
            localBack.Normalize();

            float dist = Mathf.Clamp(strength, 0.16f, 0.45f);
            _proceduralMotionSeq = DOTween.Sequence().SetUpdate(true);
            _proceduralMotionSeq.Append(_visualRoot.DOLocalMove(_visualHomeLocalPos + localBack * dist + new Vector3(0f, 0.08f, 0f), duration * 0.28f).SetEase(Ease.OutExpo));
            _proceduralMotionSeq.Join(_visualRoot.DOScale(new Vector3(_visualHomeLocalScale.x * 1.14f, _visualHomeLocalScale.y * 0.84f, _visualHomeLocalScale.z), duration * 0.28f));
            _proceduralMotionSeq.Append(_visualRoot.DOLocalMove(_visualHomeLocalPos, duration * 0.72f).SetEase(Ease.OutElastic));
            _proceduralMotionSeq.Join(_visualRoot.DOScale(_visualHomeLocalScale, duration * 0.72f).SetEase(Ease.OutBack));
            _proceduralMotionSeq.OnComplete(() => _isPlayingActionMotion = false);
        }

        public Sequence PlayProceduralDeathDissolve(float duration = 0.36f)
        {
            CaptureProceduralVisualHomeIfNeeded();
            _proceduralMotionSeq?.Kill();
            _isPlayingActionMotion = true;
            ClearDamagePreview();

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            if (_visualRoot != null)
            {
                seq.Join(_visualRoot.DOLocalRotate(new Vector3(0f, 0f, this is EnemyView ? -18f : 18f), duration).SetEase(Ease.InQuad));
                seq.Join(_visualRoot.DOLocalMoveY(_visualHomeLocalPos.y + 0.35f, duration).SetEase(Ease.OutCubic));
                seq.Join(_visualRoot.DOScale(new Vector3(_visualHomeLocalScale.x * 1.18f, 0f, _visualHomeLocalScale.z), duration).SetEase(Ease.InBack));
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.DOKill();
                spriteRenderer.color = new Color(1f, 0.42f, 0.36f, 1f);
                seq.Join(spriteRenderer.DOFade(0f, duration).SetEase(Ease.InQuad));
            }
            foreach (SpriteRenderer sr in additionalSpriteRenderers)
            {
                if (sr == null) continue;
                sr.DOKill();
                seq.Join(sr.DOFade(0f, duration).SetEase(Ease.InQuad));
            }
            _proceduralMotionSeq = seq;
            return seq;
        }

        public void ShowDamagePreview(int incomingDamage)
        {
            _previewIncomingDamage = Mathf.Max(0, incomingDamage);
            if (!useSlider || healthSlider == null || healthSlider.fillRect == null || _previewIncomingDamage <= 0 || CurrentHealth <= 0)
            {
                ClearDamagePreview();
                return;
            }

            int hpLoss = Mathf.Max(0, _previewIncomingDamage - CurrentShield);
            if (hpLoss <= 0)
            {
                ClearDamagePreview();
                return;
            }

            EnsureDamagePreviewOverlay();
            if (_damagePreviewFill == null) return;

            float currentNorm = Mathf.Clamp01((float)CurrentHealth / Mathf.Max(1, MaxHealth));
            float afterNorm = Mathf.Clamp01((float)Mathf.Max(0, CurrentHealth - hpLoss) / Mathf.Max(1, MaxHealth));

            RectTransform rt = _damagePreviewFill.rectTransform;
            rt.anchorMin = new Vector2(afterNorm, 0f);
            rt.anchorMax = new Vector2(currentNorm, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            bool lethal = hpLoss >= CurrentHealth;
            _damagePreviewFill.gameObject.SetActive(true);
            float pulse = 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 8.5f);
            _damagePreviewFill.color = lethal
                ? new Color(1f, 0.18f, 0.24f, pulse)
                : new Color(1f, 0.88f, 0.32f, pulse * 0.92f);
        }

        public void ClearDamagePreview()
        {
            _previewIncomingDamage = 0;
            if (_damagePreviewFill != null) _damagePreviewFill.gameObject.SetActive(false);
        }

        private void EnsureDamagePreviewOverlay()
        {
            if (_damagePreviewFill != null || healthSlider == null || healthSlider.fillRect == null) return;
            Transform fillArea = healthSlider.fillRect.parent;
            if (fillArea == null) return;

            GameObject go = new("DamagePreviewBar", typeof(RectTransform), typeof(Image));
            go.layer = fillArea.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(fillArea, false);
            rt.SetAsLastSibling();
            _damagePreviewFill = go.GetComponent<Image>();
            if (s_previewSolidSprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                s_previewSolidSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            _damagePreviewFill.sprite = s_previewSolidSprite;
            _damagePreviewFill.raycastTarget = false;
            go.SetActive(false);
        }

        public void SetCurrentHealth(int health)
        {
            CurrentHealth = Mathf.Clamp(health, 0, EffectiveMaxHealth);
            UpdateHealthVisual();
        }

        public virtual void ClearStats()
        {
            CurrentShield = 0;
            StunDuration = 0;
            TauntDuration = 0;
            HealthPenalty = 0;
            UpdateHealthVisual();
            NotifyShieldCleared();
            NotifyStatusEffectsChanged();
        }

        private void Update()
        {
            StateMachine?.Update();
        }

        private void LateUpdate()
        {
            if (combatantCanvas != null)
            {
                // Lock the canvas rotation so health bars don't fill backwards when the enemy is rotated to face the player
                combatantCanvas.transform.rotation = Quaternion.identity;
            }
        }

        public virtual void Damage(int damageAmount)
        {
            if (damageAmount > 0 && hitSfx != global::PlushieOdyssey.Audio.SfxID.None)
            {
                global::PlushieOdyssey.Audio.Tune.SFX(hitSfx);
            }

            int absorbedByShield = 0;
            if (CurrentShield >= damageAmount)
            {
                absorbedByShield = damageAmount;
                CurrentShield -= damageAmount;
                damageAmount = 0;
            }
            else
            {
                absorbedByShield = CurrentShield;
                damageAmount -= CurrentShield;
                CurrentShield = 0;
            }

            if (absorbedByShield > 0)
            {
                NotifyShieldHit(absorbedByShield);
                if (CurrentShield <= 0) NotifyShieldBroken();
            }

            if (damageAmount > 0)
            {
                ReduceHealth(damageAmount);
            }
            else
            {
                UpdateHealthVisual();
            }
        }

        protected void ReduceHealth(int amount)
        {
            CurrentHealth -= amount;

            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                UpdateHealthVisual();
                StateMachine.ChangeState(new CombatantDeadState(this));
            }
            else
            {
                UpdateHealthVisual();
                StateMachine.ChangeState(new CombatantHurtState(this, shakeDuration, shakeStrength));
            }
        }

        public void Heal(int healAmount)
        {
            if (healAmount > 0 && healSfx != global::PlushieOdyssey.Audio.SfxID.None)
            {
                global::PlushieOdyssey.Audio.Tune.SFX(healSfx);
            }

            CurrentHealth += healAmount;
            if (CurrentHealth > EffectiveMaxHealth)
            {
                CurrentHealth = EffectiveMaxHealth;
            }
            UpdateHealthVisual();

            StateMachine.ChangeState(new CombatantHealState(this));
        }

        public void ApplyHealthPenalty(int amount)
        {
            HealthPenalty += amount;
            if (HealthPenalty >= MaxHealth) HealthPenalty = MaxHealth - 1; // Always keep at least 1 max HP

            if (CurrentHealth > EffectiveMaxHealth)
            {
                CurrentHealth = EffectiveMaxHealth;
            }
            UpdateHealthVisual();
        }

        public void ApplyStun(int duration)
        {
            StunDuration += duration;
            NotifyStatusEffectsChanged();
        }

        public void DecreaseStun()
        {
            if (StunDuration > 0)
            {
                StunDuration--;
                NotifyStatusEffectsChanged();
            }
        }

        public void AddShield(int amount)
        {
            if (amount > 0 && shieldSfx != global::PlushieOdyssey.Audio.SfxID.None)
            {
                global::PlushieOdyssey.Audio.Tune.SFX(shieldSfx);
            }

            CurrentShield += amount;
            UpdateHealthVisual();
            NotifyShieldDeployed(amount);
        }

        public void ClearShield()
        {
            bool hadShield = CurrentShield > 0;
            CurrentShield = 0;
            UpdateHealthVisual();
            if (hadShield) NotifyShieldCleared();
        }

        public void ApplyTaunt(int duration)
        {
            TauntDuration += duration;
            NotifyStatusEffectsChanged();
        }

        public void DecreaseTaunt()
        {
            if (TauntDuration > 0)
            {
                TauntDuration--;
                NotifyStatusEffectsChanged();
            }
        }

        public virtual void AddStress(int amount) { }

        protected void NotifyStatusEffectsChanged()
        {
            StatusEffectsChanged?.Invoke();
        }

        public void SetAnimationTrigger(string trigger)
        {
            if (string.IsNullOrEmpty(trigger)) return;

            if (animator != null)
            {
                animator.SetTrigger(trigger);
            }

            if (additionalAnimators != null)
            {
                foreach (var additionalAnimator in additionalAnimators)
                {
                    if (additionalAnimator != null)
                    {
                        additionalAnimator.SetTrigger(trigger);
                    }
                }
            }
        }

        public void DoHurtFlash(float duration = 0.2f)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.DOKill();
                spriteRenderer.color = Color.red;
                spriteRenderer.DOColor(Color.white, duration);
            }

            if (additionalSpriteRenderers != null)
            {
                foreach (var sr in additionalSpriteRenderers)
                {
                    if (sr != null)
                    {
                        sr.DOKill();
                        sr.color = Color.red;
                        sr.DOColor(Color.white, duration);
                    }
                }
            }
        }

        protected virtual void SetupHealthVisual()
        {
            combatantCanvas = GetComponentInChildren<Canvas>();
            hasHealthVisual = !HealthObjectIsNull();
            if (!hasHealthVisual) return;

            if (healthText != null) healthText.gameObject.SetActive(!useSlider);

            List<Slider> sliders = new() { healthSlider, blockedSlider, shieldSlider };
            foreach (var slider in sliders)
            {
                if (slider == null) continue;
                slider.gameObject.SetActive(useSlider);
            }
        }

        protected virtual void UpdateHealthVisual(bool instant = false)
        {
            HealthChanged?.Invoke();
            if (!hasHealthVisual) return;

            if (useSlider)
            {
                float targetHealth = Mathf.Clamp01((float)CurrentHealth / MaxHealth);
                healthSlider.DOKill();
                if (instant) healthSlider.value = targetHealth;
                else healthSlider.DOValue(targetHealth, 0.3f);

                float targetBlocked = Mathf.Clamp01((float)HealthPenalty / MaxHealth);
                if (blockedSlider != null)
                {
                    blockedSlider.DOKill();
                    if (instant) blockedSlider.value = targetBlocked;

                    if (blockedSlider.fillRect != null)
                    {
                        if (HealthPenalty > 0)
                        {
                            blockedSlider.fillRect.gameObject.SetActive(true);
                            if (!instant) blockedSlider.DOValue(targetBlocked, 0.3f);
                        }
                        else
                        {
                            if (instant || blockedSlider.value <= 0.001f)
                            {
                                blockedSlider.value = 0f;
                                blockedSlider.fillRect.gameObject.SetActive(false);
                            }
                            else
                            {
                                blockedSlider.DOValue(0f, 0.3f).OnComplete(() =>
                                {
                                    if (blockedSlider != null && blockedSlider.fillRect != null && blockedSlider.value <= 0.001f)
                                        blockedSlider.fillRect.gameObject.SetActive(false);
                                });
                            }
                        }
                    }
                    else if (!instant)
                    {
                        blockedSlider.DOValue(targetBlocked, 0.3f);
                    }
                }

                float targetShield = Mathf.Clamp01((float)CurrentShield / MaxHealth);
                if (shieldSlider != null)
                {
                    shieldSlider.DOKill();
                    if (instant) shieldSlider.value = targetShield;

                    if (shieldSlider.fillRect != null)
                    {
                        if (CurrentShield > 0)
                        {
                            shieldSlider.fillRect.gameObject.SetActive(true);
                            if (!instant) shieldSlider.DOValue(targetShield, 0.3f);
                        }
                        else
                        {
                            if (instant || shieldSlider.value <= 0.001f)
                            {
                                shieldSlider.value = 0f;
                                shieldSlider.fillRect.gameObject.SetActive(false);
                            }
                            else
                            {
                                shieldSlider.DOValue(0f, 0.3f).OnComplete(() =>
                                {
                                    if (shieldSlider != null && shieldSlider.fillRect != null && shieldSlider.value <= 0.001f)
                                        shieldSlider.fillRect.gameObject.SetActive(false);
                                });
                            }
                        }
                    }
                    else if (!instant)
                    {
                        shieldSlider.DOValue(targetShield, 0.3f);
                    }
                }
            }
            else
            {
                string shieldText = CurrentShield > 0 ? $" [+{CurrentShield}]" : "";
                string penaltyText = HealthPenalty > 0 ? $" (Blocked: {HealthPenalty})" : "";
                healthText.text = $"HP: {CurrentHealth}/{EffectiveMaxHealth}{shieldText}{penaltyText}";
            }

        }

        protected bool HealthObjectIsNull()
        {
            if (useSlider)
            {
                if (healthSlider == null)
                {
                    global::PlushieOdyssey.Core.Logger.LogError(this, $"[{name}] Health slider is not assigned or null!");
                    return true;
                }
            }
            else
            {
                if (healthText == null)
                {
                    global::PlushieOdyssey.Core.Logger.LogError(this, $"[{name}] Any slider is not assigned or null!");
                    return true;
                }
            }

            return false;
        }
    }
}
