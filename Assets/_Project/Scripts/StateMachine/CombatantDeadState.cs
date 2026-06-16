using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatantDeadState : CombatantState
{
    public CombatantDeadState(CombatantView combatant, Action onComplete = null) : base(combatant, onComplete) { }

    public override void Enter()
    {
        // Optional death visual effect
        onComplete?.Invoke();
    }
}
