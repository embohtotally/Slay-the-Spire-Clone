using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CombatantView : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [SerializeField] private float shakeDuration;
    [SerializeField] private float shakeStrength;

    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }

    public int StunDuration { get; private set; }
    public bool IsStunned => StunDuration > 0;

    public int CurrentShield { get; private set; }
    
    public int TauntDuration { get; private set; }
    public bool IsTaunted => TauntDuration > 0;

    public float ShakeDuration => shakeDuration;
    public float ShakeStrength => shakeStrength;

    public StateMachine StateMachine { get; private set; }

    protected void SetupBase(int maxHealth, Sprite image)
    {
        MaxHealth = CurrentHealth = maxHealth;
        spriteRenderer.sprite = image;
        
        StateMachine = new StateMachine();
        StateMachine.Initialize(new CombatantIdleState(this));

        UpdateHealthText();
    }

    public void SetCurrentHealth(int health)
    {
        CurrentHealth = health;
        UpdateHealthText();
    }

    public virtual void ClearStats()
    {
        CurrentShield = 0;
        StunDuration = 0;
        TauntDuration = 0;
        UpdateHealthText();
    }

    private void Update()
    {
        StateMachine?.Update();
    }

    public virtual void Damage(int damageAmount)
    {
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
            UpdateHealthText();
        }
    }

    protected void ReduceHealth(int amount)
    {
        CurrentHealth -= amount;

        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            UpdateHealthText();
            StateMachine.ChangeState(new CombatantDeadState(this));
        }
        else
        {
            UpdateHealthText();
            StateMachine.ChangeState(new CombatantHurtState(this, shakeDuration, shakeStrength));
        }
    }

    public void Heal(int healAmount)
    {
        CurrentHealth += healAmount;
        if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
        }
        UpdateHealthText();
    }

    public void ApplyStun(int duration)
    {
        StunDuration += duration;
    }

    public void DecreaseStun()
    {
        if (StunDuration > 0)
        {
            StunDuration--;
        }
    }

    public void AddShield(int amount)
    {
        CurrentShield += amount;
        UpdateHealthText();
    }

    public void ClearShield()
    {
        CurrentShield = 0;
        UpdateHealthText();
    }

    public void ApplyTaunt(int duration)
    {
        TauntDuration += duration;
    }

    public void DecreaseTaunt()
    {
        if (TauntDuration > 0)
        {
            TauntDuration--;
        }
    }

    public virtual void AddStress(int amount) { }

    protected virtual void UpdateHealthText()
    {
        string shieldText = CurrentShield > 0 ? $" [+{CurrentShield}]" : "";
        healthText.text = $"HP: {CurrentHealth}{shieldText}";
    }
}