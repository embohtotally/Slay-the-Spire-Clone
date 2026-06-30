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

        if (combatant.Animator != null)
        {
            combatant.Animator.SetTrigger("Hurt");
        }
        else
        {
            combatant.transform.DOShakePosition(shakeDuration, shakeStrength).OnComplete(CompleteHurt);
        }
    }

    public override void Update()
    {
        if (isComplete) return;

        if (combatant.Animator != null)
        {
            timer += Time.deltaTime;
            if (timer >= shakeDuration)
            {
                CompleteHurt();
            }
        }
    }

    private void CompleteHurt()
    {
        if (isComplete) return;
        isComplete = true;

        onComplete?.Invoke();
        combatant.StateMachine.ChangeState(new CombatantIdleState(combatant));
    }
}
