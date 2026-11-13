using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.States
{
    public class CombatantIdleState : CombatantState
    {
        public CombatantIdleState(CombatantView combatant) : base(combatant) { }

        public override void Enter()
        {
            // Just idling
        }
    }
}
