using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatantDeadState : CombatantState
{
    public CombatantDeadState(CombatantView combatant, Action onComplete = null) : base(combatant, onComplete) { }

    public override void Enter()
    {
        if (combatant.Animator != null && !string.IsNullOrEmpty(combatant.DeadAnimationTrigger))
        {
            combatant.Animator.SetTrigger(combatant.DeadAnimationTrigger);
        }

        onComplete?.Invoke();
    }
}
