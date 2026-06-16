using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CombatantView : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float shakeDuration;
    [SerializeField] private float shakeStrength;

    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }

    public float ShakeDuration => shakeDuration;
    public float ShakeStrength => shakeStrength;

    public StateMachine StateMachine { get; private set; }

    protected void SetupBase(int health, Sprite image)
    {
        MaxHealth = CurrentHealth = health;
        spriteRenderer.sprite = image;
        
        StateMachine = new StateMachine();
        StateMachine.Initialize(new CombatantIdleState(this));

        UpdateHealthText();
    }

    private void Update()
    {
        StateMachine?.Update();
    }

    public void Damage(int damageAmount)
    {
        CurrentHealth -= damageAmount;

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

    private void UpdateHealthText()
    {
        healthText.text = "HP: " + CurrentHealth;
    }
}