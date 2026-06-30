using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BuffSystem : Singleton<BuffSystem>
{
    private List<BuffData> activeBuffs = new();

    public event Action StatusEffectsChanged;

    public IReadOnlyList<BuffData> ActiveBuffs => activeBuffs;

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<ApplyBuffGA>(ApplyBuffPerformer);
        ActionSystem.AttachPerformer<ApplyStatusGA>(ApplyStatusPerformer);
        ActionSystem.AttachPerformer<RemoveDebuffGA>(RemoveDebuffPerformer);
        ActionSystem.SubscribeReaction<DealDamageGA>(OnDealDamagePreReaction, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPreReaction, ReactionTiming.PRE);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<ApplyBuffGA>();
        ActionSystem.DetachPerformer<ApplyStatusGA>();
        ActionSystem.DetachPerformer<RemoveDebuffGA>();
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
        NotifyStatusEffectsChanged();
        yield return null;
    }

    private IEnumerator ApplyStatusPerformer(ApplyStatusGA applyStatusGA)
    {
        BuffType mappedType = BuffType.Vulnerable;
        if (applyStatusGA.Status == StatusType.Weakness) mappedType = BuffType.Weak;
        else if (applyStatusGA.Status == StatusType.Poison) mappedType = BuffType.Poison;

        foreach (CombatantView target in applyStatusGA.Targets)
        {
            BuffData buff = new BuffData(target, mappedType, applyStatusGA.Stacks, applyStatusGA.Duration, applyStatusGA.Caster);
            activeBuffs.Add(buff);
        }
        NotifyStatusEffectsChanged();
        yield return null;
    }

    private IEnumerator RemoveDebuffPerformer(RemoveDebuffGA removeDebuffGA)
    {
        foreach (CombatantView target in removeDebuffGA.Targets)
        {
            List<BuffData> targetBuffs = GetBuffsFor(target);
            targetBuffs.Reverse();
            int removedCount = 0;

            foreach (BuffData buff in targetBuffs)
            {
                if (IsDebuff(buff.Type))
                {
                    activeBuffs.Remove(buff);
                    removedCount++;
                    if (removedCount >= removeDebuffGA.Count) break;
                }
            }
        }
        NotifyStatusEffectsChanged();
        yield return null;
    }

    private bool IsDebuff(BuffType type)
    {
        return type == BuffType.Vulnerable || type == BuffType.Weak || type == BuffType.Poison;
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

        if (expiredBuffs.Count > 0)
        {
            NotifyStatusEffectsChanged();
        }
    }

    public List<BuffData> GetBuffsFor(CombatantView target)
    {
        List<BuffData> results = new();
        if (target == null) return results;

        foreach (BuffData buff in activeBuffs)
        {
            if (buff.Target == target && buff.RemainingTurns > 0)
            {
                results.Add(buff);
            }
        }

        return results;
    }

    private void NotifyStatusEffectsChanged()
    {
        StatusEffectsChanged?.Invoke();
    }
}
