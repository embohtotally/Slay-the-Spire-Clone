using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatantAttackState : CombatantState
{
    private float xOffset;
    private float moveDuration;
    private float returnDuration;

    public CombatantAttackState(CombatantView combatant, float xOffset, float moveDuration, float returnDuration, Action onComplete = null) 
        : base(combatant, onComplete)
    {
        this.xOffset = xOffset;
        this.moveDuration = moveDuration;
        this.returnDuration = returnDuration;
    }

    public override void Enter()
    {
        Vector3 startPos = combatant.transform.position;
        Sequence attackSequence = DOTween.Sequence();
        
        attackSequence.Append(combatant.transform.DOMoveX(startPos.x + xOffset, moveDuration));
        attackSequence.Append(combatant.transform.DOMoveX(startPos.x, returnDuration));
        
        attackSequence.OnComplete(() =>
        {
            onComplete?.Invoke();
            combatant.StateMachine.ChangeState(new CombatantIdleState(combatant));
        });
    }
}
