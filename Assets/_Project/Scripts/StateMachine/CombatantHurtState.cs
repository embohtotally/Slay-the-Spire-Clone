using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatantHurtState : CombatantState
{
    private float shakeDuration;
    private float shakeStrength;
    private float timer;
    private bool isComplete;

    public CombatantHurtState(CombatantView combatant, float shakeDuration, float shakeStrength, Action onComplete = null) 
        : base(combatant, onComplete) 
    { 
        this.shakeDuration = shakeDuration;
        this.shakeStrength = shakeStrength;
    }

    public override void Enter()
    {
        timer = 0f;
        isComplete = false;

        if (combatant.SpriteRenderer != null)
        {
            combatant.SpriteRenderer.DOKill();
            combatant.SpriteRenderer.color = Color.red;
            combatant.SpriteRenderer.DOColor(Color.white, 0.2f);
        }

        if (combatant.Animator != null && !string.IsNullOrEmpty(combatant.HurtAnimationTrigger))
        {
            combatant.Animator.SetTrigger(combatant.HurtAnimationTrigger);
            CompleteHurt(); // Instantly finish the state (fire and forget)
        }
        else
        {
            combatant.transform.DOShakePosition(shakeDuration, shakeStrength).OnComplete(CompleteHurt);
        }
    }

    public override void Update()
    {
        // No update logic needed anymore for Animator, as it instantly completes in Enter()
    }

    private void CompleteHurt()
    {
        if (isComplete) return;
        isComplete = true;

        onComplete?.Invoke();
        combatant.StateMachine.ChangeState(new CombatantIdleState(combatant));
    }
}
