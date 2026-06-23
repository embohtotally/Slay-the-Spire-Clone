using DG.Tweening;
using System;
using UnityEngine;

public class CombatantBuffState : CombatantState
{
    private float buffDuration;
    private float scaleMultiplier;

    // Constructor to pass in the context and animation parameters
    public CombatantBuffState(CombatantView combatant, float buffDuration = 0.5f, float scaleMultiplier = 1.2f, Action onComplete = null) 
        : base(combatant, onComplete)
    {
        this.buffDuration = buffDuration;
        this.scaleMultiplier = scaleMultiplier;
    }

    public override void Enter()
    {
        Vector3 originalScale = combatant.transform.localScale;
        Vector3 targetScale = originalScale * scaleMultiplier;

        // Create a DOTween sequence to scale up, then scale back down
        Sequence buffSequence = DOTween.Sequence();
        buffSequence.Append(combatant.transform.DOScale(targetScale, buffDuration / 2));
        buffSequence.Append(combatant.transform.DOScale(originalScale, buffDuration / 2));

        // When the animation sequence finishes...
        buffSequence.OnComplete(() =>
        {
            // 1. Invoke the callback so the battle system knows we are done animating
            onComplete?.Invoke(); 
            
            // 2. Automatically transition back to the Idle state
            combatant.StateMachine.ChangeState(new CombatantIdleState(combatant)); 
        });
    }
    
    // Optional: If you need to stop the animation early when exiting the state prematurely
    public override void Exit()
    {
        // DOTween.Kill(combatant.transform); // (Be careful killing tweens globally like this, usually you'd cache the sequence to kill it specifically)
    }
}
