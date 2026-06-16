using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatantHurtState : CombatantState
{
    private float shakeDuration;
    private float shakeStrength;

    public CombatantHurtState(CombatantView combatant, float shakeDuration, float shakeStrength, Action onComplete = null) 
        : base(combatant, onComplete) 
    { 
        this.shakeDuration = shakeDuration;
        this.shakeStrength = shakeStrength;
    }

    public override void Enter()
    {
        combatant.transform.DOShakePosition(shakeDuration, shakeStrength).OnComplete(() =>
        {
            onComplete?.Invoke();
            combatant.StateMachine.ChangeState(new CombatantIdleState(combatant));
        });
    }
}
