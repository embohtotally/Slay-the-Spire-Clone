using System.Collections;
using UnityEngine;

[System.Serializable]
public class StunSystem : MonoBehaviour
{
    private void OnEnable()
    {
        ActionSystem.AttachPerformer<ApplyStunGA>(ApplyStunPerformer);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(EnemyTurnPostReaction, ReactionTiming.POST);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<ApplyStunGA>();
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(EnemyTurnPostReaction, ReactionTiming.POST);
    }

    private IEnumerator ApplyStunPerformer(ApplyStunGA applyStunGA)
    {
        foreach (CombatantView target in applyStunGA.Targets)
        {
            target.ApplyStun(applyStunGA.Duration);
        }
        
        yield return null;
    }

    private void EnemyTurnPostReaction(EnemyTurnGA enemyTurnGA)
    {
        if (HeroSystem.Instance.HeroView.IsStunned)
        {
            HeroSystem.Instance.HeroView.DecreaseStun();
            // Start enemy turn again to simulate skipping player turn
            EnemyTurnGA newEnemyTurnGA = new();
            ActionSystem.Instance.AddReaction(newEnemyTurnGA);
        }
    }
}
