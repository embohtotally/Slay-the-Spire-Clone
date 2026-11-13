using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core.StateMachines;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.States
{
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
}
