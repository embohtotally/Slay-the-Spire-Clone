using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatantIdleState : CombatantState
{
    public CombatantIdleState(CombatantView combatant) : base(combatant) { }

    public override void Enter()
    {
        // Just idling
    }
}
