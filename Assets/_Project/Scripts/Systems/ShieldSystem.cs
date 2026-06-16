using System.Collections;
using UnityEngine;

[System.Serializable]
public class ShieldSystem : MonoBehaviour
{
    private void OnEnable()
    {
        ActionSystem.AttachPerformer<GainShieldGA>(GainShieldPerformer);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(EnemyTurnPreReaction, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(EnemyTurnPostReaction, ReactionTiming.POST);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<GainShieldGA>();
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(EnemyTurnPreReaction, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(EnemyTurnPostReaction, ReactionTiming.POST);
    }

    private IEnumerator GainShieldPerformer(GainShieldGA gainShieldGA)
    {
        foreach (CombatantView target in gainShieldGA.Targets)
        {
            target.AddShield(gainShieldGA.Amount);
        }
        
        yield return null;
    }

    private void EnemyTurnPreReaction(EnemyTurnGA enemyTurnGA)
    {
        // Enemies lose shield at the start of their turn
        if (EnemySystem.Instance != null && EnemySystem.Instance.Enemies != null)
        {
            foreach (EnemyView enemy in EnemySystem.Instance.Enemies)
            {
                enemy.ClearShield();
            }
        }
    }

    private void EnemyTurnPostReaction(EnemyTurnGA enemyTurnGA)
    {
        // Player loses shield at the start of their turn
        if (HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null)
        {
            HeroSystem.Instance.HeroView.ClearShield();
        }
    }
}
