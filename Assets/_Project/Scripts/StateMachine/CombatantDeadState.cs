using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.States
{
    public class CombatantDeadState : CombatantState
    {
        public CombatantDeadState(CombatantView combatant, Action onComplete = null) : base(combatant, onComplete) { }

        public override void Enter()
        {
            if (combatant.HasAnimator && !string.IsNullOrEmpty(combatant.DeadAnimationTrigger))
            {
                combatant.SetAnimationTrigger(combatant.DeadAnimationTrigger);
            }

            onComplete?.Invoke();
        }
    }
}
