using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class CombatantState : State
{
    protected CombatantView combatant;
    protected Action onComplete;

    public CombatantState(CombatantView combatant, Action onComplete = null)
    {
        this.combatant = combatant;
        this.onComplete = onComplete;
    }
}
