using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatantDeadState : CombatantState
{
    public CombatantDeadState(CombatantView combatant, Action onComplete = null) : base(combatant, onComplete) { }

    public override void Enter()
    {
        if (combatant.Animator != null)
        {
            combatant.Animator.SetTrigger("Die");
        }

        onComplete?.Invoke();
    }
}
