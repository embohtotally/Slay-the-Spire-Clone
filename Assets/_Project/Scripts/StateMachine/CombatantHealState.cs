using DG.Tweening;
using System;
using UnityEngine;

public class CombatantHealState : CombatantState
{
    private float animationDuration;
    private bool isComplete;

    public CombatantHealState(CombatantView combatant, float animationDuration = 0.4f, Action onComplete = null) 
        : base(combatant, onComplete)
    {
        this.animationDuration = animationDuration;
    }

    public override void Enter()
    {
        isComplete = false;

        if (combatant.SpriteRenderer != null)
        {
            combatant.SpriteRenderer.DOKill();
            combatant.SpriteRenderer.color = Color.green;
            combatant.SpriteRenderer.DOColor(Color.white, animationDuration);
            
            combatant.transform.DOKill();
            combatant.transform.DOPunchScale(Vector3.one * 0.15f, animationDuration, 1, 0.5f)
                .OnComplete(CompleteHeal);
        }
        else
        {
            CompleteHeal();
        }
    }

    private void CompleteHeal()
    {
        if (isComplete) return;
        isComplete = true;

        onComplete?.Invoke();
        combatant.StateMachine.ChangeState(new CombatantIdleState(combatant));
    }
}
