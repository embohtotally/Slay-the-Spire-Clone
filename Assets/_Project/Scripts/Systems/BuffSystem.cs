using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BuffSystem : Singleton<BuffSystem>
{
    private List<BuffData> activeBuffs = new();

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<ApplyBuffGA>(ApplyBuffPerformer);
        ActionSystem.SubscribeReaction<DealDamageGA>(OnDealDamagePreReaction, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPreReaction, ReactionTiming.PRE);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<ApplyBuffGA>();
        ActionSystem.UnsubscribeReaction<DealDamageGA>(OnDealDamagePreReaction, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPreReaction, ReactionTiming.PRE);
    }

    private IEnumerator ApplyBuffPerformer(ApplyBuffGA applyBuffGA)
    {
        foreach (CombatantView target in applyBuffGA.Targets)
        {
            BuffData buff = new BuffData(target, applyBuffGA.BuffType, applyBuffGA.Value, applyBuffGA.Duration, applyBuffGA.Caster);
            activeBuffs.Add(buff);
        }
        yield return null;
    }

    private void OnDealDamagePreReaction(DealDamageGA dealDamageGA)
    {
        // Outgoing damage modifiers (Caster)
        if (dealDamageGA.Caster != null)
        {
            foreach (BuffData buff in activeBuffs)
            {
                if (buff.Target == dealDamageGA.Caster && buff.RemainingTurns > 0)
                {
                    if (buff.Type == BuffType.Strength)
                    {
                        dealDamageGA.Amount += buff.Value;
                    }
                    else if (buff.Type == BuffType.Weak)
                    {
                        dealDamageGA.Amount = Mathf.FloorToInt(dealDamageGA.Amount * 0.75f);
                    }
                    else if (buff.Type == BuffType.DamageReductionPercentage)
                    {
                        float multiplier = Mathf.Max(0f, 1f - (buff.Value / 100f));
                        dealDamageGA.Amount = Mathf.Max(0, Mathf.FloorToInt(dealDamageGA.Amount * multiplier));
                    }
                }
            }
        }

        // Incoming damage modifiers (Targets)
        // Since DealDamageGA has a single Amount for all targets, if there are multiple targets we check if any target has modifiers,
        // or typically in single target attacks it modifies Amount perfectly.
        foreach (CombatantView target in dealDamageGA.Targets)
        {
            foreach (BuffData buff in activeBuffs)
            {
                if (buff.Target == target && buff.RemainingTurns > 0)
                {
                    if (buff.Type == BuffType.Vulnerable)
                    {
                        dealDamageGA.Amount = Mathf.FloorToInt(dealDamageGA.Amount * 1.5f);
                    }
                    else if (buff.Type == BuffType.DamageReduction)
                    {
                        dealDamageGA.Amount = Mathf.Max(0, dealDamageGA.Amount - buff.Value);
                    }
                }
            }
        }
    }

    private void OnEnemyTurnPreReaction(EnemyTurnGA enemyTurnGA)
    {
        List<BuffData> expiredBuffs = new();

        foreach (BuffData buff in activeBuffs)
        {
            buff.DecrementDuration();
            if (buff.RemainingTurns <= 0 || buff.Target == null || buff.Target.CurrentHealth <= 0)
            {
                expiredBuffs.Add(buff);
            }
        }

        foreach (BuffData buff in expiredBuffs)
        {
            activeBuffs.Remove(buff);
        }
    }
}
