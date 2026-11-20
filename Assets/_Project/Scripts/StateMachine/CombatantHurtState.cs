using PlushieOdyssey.Combat.Views;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.States
{
    public class CombatantHurtState : CombatantState
    {
        private float shakeDuration;
        private float shakeStrength;
        private bool isComplete;

        public CombatantHurtState(CombatantView combatant, float shakeDuration, float shakeStrength, Action onComplete = null)
            : base(combatant, onComplete)
        {
            this.shakeDuration = shakeDuration;
            this.shakeStrength = shakeStrength;
        }

        public override void Enter()
        {
            isComplete = false;

            combatant.DoHurtFlash(0.22f);
            combatant.PlayProceduralHurtRecoil(Mathf.Max(0.18f, shakeDuration), Mathf.Max(0.18f, shakeStrength));

            if (combatant.HasAnimator && !string.IsNullOrEmpty(combatant.HurtAnimationTrigger))
            {
                combatant.SetAnimationTrigger(combatant.HurtAnimationTrigger);
            }

            CompleteHurt();
        }

        public override void Update()
        {
            // No update logic needed anymore for Animator, as it instantly completes in Enter()
        }

        private void CompleteHurt()
        {
            if (isComplete) return;
            isComplete = true;

            onComplete?.Invoke();
            combatant.StateMachine.ChangeState(new CombatantIdleState(combatant));
        }
    }
}
