using System.Collections;
using UnityEngine;
using DG.Tweening;

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
            Gameseed26.GameManager.GenerateFloatingText("+" + gainShieldGA.Amount, target.transform, 1f, 1f, "#00BFFF");
            target.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.2f, 5, 1f);
        }
        
        yield return new WaitForSeconds(0.4f);
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
