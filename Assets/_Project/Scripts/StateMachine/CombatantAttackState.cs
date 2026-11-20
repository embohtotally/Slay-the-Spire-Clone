using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.States
{
    public class CombatantAttackState : CombatantState
    {
        private float animationDuration;
        private float timer;
        private bool isComplete;
        private Vector3? targetWorldPos;

        public CombatantAttackState(CombatantView combatant, float animationDuration, Action onComplete = null, Vector3? targetWorldPos = null)
            : base(combatant, onComplete)
        {
            this.animationDuration = animationDuration;
            this.targetWorldPos = targetWorldPos;
        }

        public override void Enter()
        {
            timer = 0f;
            isComplete = false;

            if (combatant.HasAnimator)
            {
                combatant.SetAnimationTrigger("Attack");
            }

            // Always play snappy procedural wind-up & directional lunge so static sprites feel alive!
            Vector3 lungeTarget = targetWorldPos ?? (combatant.transform.position + (combatant is EnemyView ? Vector3.left * 2.2f : Vector3.right * 2.2f));
            combatant.PlayProceduralAttackLunge(lungeTarget, Mathf.Clamp(animationDuration, 0.24f, 0.55f));
        }

        public override void Update()
        {
            if (isComplete) return;

            timer += Time.deltaTime;
            if (timer >= animationDuration)
            {
                CompleteAttack();
            }
        }

        private void CompleteAttack()
        {
            isComplete = true;
            onComplete?.Invoke();
            combatant.StateMachine.ChangeState(new CombatantIdleState(combatant));
        }
    }
}
