using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DamageOverTimeSystem : Singleton<DamageOverTimeSystem>
{
    private List<DamageOverTimeData> activeDoTs = new();

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<ApplyDoTGA>(ApplyDoTPerformer);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPreReaction, ReactionTiming.PRE);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<ApplyDoTGA>();
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPreReaction, ReactionTiming.PRE);
    }

    private IEnumerator ApplyDoTPerformer(ApplyDoTGA applyDoTGA)
    {
        foreach (CombatantView target in applyDoTGA.Targets)
        {
            // Check if target already has DoT from same caster? For simplicity, we just add it to the list.
            // A more advanced system might stack durations or damage. We will stack damage for simplicity by adding multiple DoT instances.
            DamageOverTimeData dot = new DamageOverTimeData(target, applyDoTGA.DamagePerTurn, applyDoTGA.Duration, applyDoTGA.Caster);
            activeDoTs.Add(dot);
        }
        yield return null;
    }

    private void OnEnemyTurnPreReaction(EnemyTurnGA enemyTurnGA)
    {
        // Trigger all active DoTs
        List<DamageOverTimeData> expiredDoTs = new();

        foreach (DamageOverTimeData dot in activeDoTs)
        {
            if (dot.Target != null && dot.Target.CurrentHealth > 0)
            {
                // Deal Damage
                DealDamageGA dealDamageGA = new(dot.DamagePerTurn, new List<CombatantView> { dot.Target }, dot.Caster);
                ActionSystem.Instance.AddReaction(dealDamageGA);
            }

            dot.DecrementDuration();
            if (dot.RemainingTurns <= 0 || dot.Target == null || dot.Target.CurrentHealth <= 0)
            {
                expiredDoTs.Add(dot);
            }
        }

        foreach (DamageOverTimeData dot in expiredDoTs)
        {
            activeDoTs.Remove(dot);
        }
    }
}
