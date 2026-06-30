using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatantAttackState : CombatantState
{
    private float animationDuration;
    private float timer;
    private bool isComplete;

    public CombatantAttackState(CombatantView combatant, float animationDuration, Action onComplete = null) 
        : base(combatant, onComplete)
    {
        this.animationDuration = animationDuration;
    }

    public override void Enter()
    {
        timer = 0f;
        isComplete = false;

        if (combatant.Animator != null)
        {
            combatant.Animator.SetTrigger("Attack");
        }
    }

    public override void Update()
    {
        if (isComplete) return;

        timer += Time.deltaTime;
        if (timer >= animationDuration)
        {
            CompleteAttack();
        }
    }

    private void CompleteAttack()
    {
        isComplete = true;
        onComplete?.Invoke();
        combatant.StateMachine.ChangeState(new CombatantIdleState(combatant));
    }
}
