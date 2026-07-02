using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Gameseed26;
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
    [SerializeField] private Animator animator;
    [SerializeField] private string hurtAnimationTrigger = "Hurt";
    [SerializeField] private string deadAnimationTrigger = "Die";
    [SerializeField] private float shakeDuration;
    [SerializeField] private float shakeStrength;
    [SerializeField] private Gameseed26.SfxID hitSfx = Gameseed26.SfxID.Hit;
    [SerializeField] private Gameseed26.SfxID healSfx = Gameseed26.SfxID.Heal;
    [SerializeField] private Gameseed26.SfxID shieldSfx = Gameseed26.SfxID.Shield;

    public Animator Animator => animator;
    public string HurtAnimationTrigger => hurtAnimationTrigger;
    public string DeadAnimationTrigger => deadAnimationTrigger;
    public SpriteRenderer SpriteRenderer => spriteRenderer;

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

    public StateMachine StateMachine { get; private set; }

    public event Action StatusEffectsChanged;

    protected void SetupBase(int maxHealth, Sprite image)
    {
        MaxHealth = CurrentHealth = maxHealth;
        spriteRenderer.sprite = image;

        StateMachine = new StateMachine();
        StateMachine.Initialize(new CombatantIdleState(this));

        SetupHealthVisual();
        UpdateHealthVisual();
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
        NotifyStatusEffectsChanged();
    }

    private void Update()
    {
        StateMachine?.Update();
    }

    public virtual void Damage(int damageAmount)
    {
        if (damageAmount > 0 && hitSfx != Gameseed26.SfxID.None)
        {
            Gameseed26.Tune.SFX(hitSfx);
        }

        if (CurrentShield >= damageAmount)
        {
            CurrentShield -= damageAmount;
            damageAmount = 0;
        }
        else
        {
            damageAmount -= CurrentShield;
            CurrentShield = 0;
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
        if (healAmount > 0 && healSfx != Gameseed26.SfxID.None)
        {
            Gameseed26.Tune.SFX(healSfx);
        }

        CurrentHealth += healAmount;
        if (CurrentHealth > EffectiveMaxHealth)
        {
            CurrentHealth = EffectiveMaxHealth;
        }
        UpdateHealthVisual();
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
        if (amount > 0 && shieldSfx != Gameseed26.SfxID.None)
        {
            Gameseed26.Tune.SFX(shieldSfx);
        }

        CurrentShield += amount;
        UpdateHealthVisual();
    }

    public void ClearShield()
    {
        CurrentShield = 0;
        UpdateHealthVisual();
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

    protected virtual void SetupHealthVisual()
    {
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

    protected virtual void UpdateHealthVisual()
    {
        if (!hasHealthVisual) return;

        if (useSlider)
        {
            float targetHealth = Mathf.Clamp01((float)CurrentHealth / MaxHealth);
            healthSlider.DOKill();
            healthSlider.DOValue(targetHealth, 0.3f);

            float targetBlocked = Mathf.Clamp01((float)HealthPenalty / MaxHealth);
            blockedSlider.DOKill();
            if (blockedSlider.fillRect != null)
            {
                if (HealthPenalty > 0)
                {
                    blockedSlider.fillRect.gameObject.SetActive(true);
                    blockedSlider.DOValue(targetBlocked, 0.3f);
                }
                else
                {
                    blockedSlider.DOValue(0f, 0.3f).OnComplete(() => 
                    {
                        if (blockedSlider.value <= 0.001f)
                            blockedSlider.fillRect.gameObject.SetActive(false);
                    });
                }
            }

            float targetShield = Mathf.Clamp01((float)CurrentShield / MaxHealth);
            shieldSlider.DOKill();
            if (shieldSlider.fillRect != null)
            {
                if (CurrentShield > 0)
                {
                    shieldSlider.fillRect.gameObject.SetActive(true);
                    shieldSlider.DOValue(targetShield, 0.3f);
                }
                else
                {
                    shieldSlider.DOValue(0f, 0.3f).OnComplete(() => 
                    {
                        if (shieldSlider.value <= 0.001f)
                            shieldSlider.fillRect.gameObject.SetActive(false);
                    });
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
            if (healthSlider == null || blockedSlider == null || shieldSlider == null)
            {
                Gameseed26.Logger.LogError(this, $"[{name}] Any slider is not assigned or null!");
                return true;
            }
        }
        else
        {
            if (healthText == null)
            {
                Gameseed26.Logger.LogError(this, $"[{name}] Any slider is not assigned or null!");
                return true;
            }
        }

        return false;
    }
}
