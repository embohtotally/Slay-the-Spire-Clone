using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [System.Serializable]
    public class BuffSystem : Singleton<BuffSystem>
    {
        private readonly List<BuffData> activeBuffs = new();

        public event Action StatusEffectsChanged;

        public IReadOnlyList<BuffData> ActiveBuffs => activeBuffs;

        private void OnEnable()
        {
            ActionSystem.AttachPerformer<ApplyBuffGA>(ApplyBuffPerformer);
            ActionSystem.AttachPerformer<ApplyStatusGA>(ApplyStatusPerformer);
            ActionSystem.AttachPerformer<RemoveDebuffGA>(RemoveDebuffPerformer);
            ActionSystem.SubscribeReaction<DealDamageGA>(OnDealDamagePreReaction, ReactionTiming.PRE);
            ActionSystem.SubscribeReaction<GainShieldGA>(OnGainShieldPreReaction, ReactionTiming.PRE);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPreReaction, ReactionTiming.PRE);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPostReaction, ReactionTiming.POST);
        }

        private void OnDisable()
        {
            ActionSystem.DetachPerformer<ApplyBuffGA>();
            ActionSystem.DetachPerformer<ApplyStatusGA>();
            ActionSystem.DetachPerformer<RemoveDebuffGA>();
            ActionSystem.UnsubscribeReaction<DealDamageGA>(OnDealDamagePreReaction, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<GainShieldGA>(OnGainShieldPreReaction, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPreReaction, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPostReaction, ReactionTiming.POST);
        }

        public bool HasBuff(CombatantView target, BuffType type)
        {
            return GetBuffStack(target, type) > 0;
        }

        public int GetBuffStack(CombatantView target, BuffType type)
        {
            if (target == null) return 0;
            int total = 0;
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                BuffData buff = activeBuffs[i];
                if (buff.Target == target && buff.Type == type && buff.RemainingTurns > 0 && buff.Value > 0)
                {
                    total += buff.Value;
                }
            }
            return total;
        }

        public bool TryConsumeArtifact(CombatantView target)
        {
            if (target == null) return false;
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                BuffData buff = activeBuffs[i];
                if (buff.Target == target && buff.Type == BuffType.Artifact && buff.RemainingTurns > 0 && buff.Value > 0)
                {
                    buff.DecrementValue(1);
                    if (buff.Value <= 0) activeBuffs.RemoveAt(i);
                    GameManager.GenerateFloatingText("Artifact!", target.transform, 1f, 1f, "#FFD54F");
                    NotifyStatusEffectsChanged();
                    return true;
                }
            }
            return false;
        }

        public void OnCombatantLostUnblockedHealth(CombatantView target)
        {
            if (target == null) return;
            bool changed = false;
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                BuffData buff = activeBuffs[i];
                if (buff.Target == target && buff.Type == BuffType.PlatedArmor && buff.RemainingTurns > 0 && buff.Value > 0)
                {
                    buff.DecrementValue(1);
                    if (buff.Value <= 0) activeBuffs.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
            if (changed) NotifyStatusEffectsChanged();
        }

        public int CalculateExpectedDamage(int baseDamage, CombatantView caster, CombatantView target)
        {
            int amount = baseDamage;

            if (caster != null)
            {
                if (caster is HeroView heroCaster && heroCaster.IsHighWill && amount > 0)
                {
                    amount = Mathf.Max(amount + 1, Mathf.RoundToInt(amount * heroCaster.HighWillBonusMultiplier));
                }

                foreach (BuffData buff in activeBuffs)
                {
                    if (buff.Target != caster || buff.RemainingTurns <= 0) continue;
                    if (buff.Type == BuffType.Strength)
                        amount += buff.Value;
                    else if (buff.Type == BuffType.Weak)
                        amount = Mathf.FloorToInt(amount * 0.75f);
                    else if (buff.Type == BuffType.DamageReductionPercentage)
                    {
                        float multiplier = Mathf.Max(0f, 1f - (buff.Value / 100f));
                        amount = Mathf.Max(0, Mathf.FloorToInt(amount * multiplier));
                    }
                }
            }

            if (target != null)
            {
                foreach (BuffData buff in activeBuffs)
                {
                    if (buff.Target != target || buff.RemainingTurns <= 0) continue;
                    if (buff.Type == BuffType.Vulnerable)
                        amount = Mathf.FloorToInt(amount * 1.5f);
                    else if (buff.Type == BuffType.DamageReduction)
                        amount = Mathf.Max(0, amount - buff.Value);
                    else if (buff.Type == BuffType.Intangible && amount > 1)
                        amount = 1;
                }
            }

            return Mathf.Max(0, amount);
        }

        public int CalculateExpectedShield(int baseShield, CombatantView target)
        {
            int amount = baseShield;
            if (target == null) return Mathf.Max(0, amount);

            int dexterity = GetBuffStack(target, BuffType.Dexterity);
            amount += dexterity;
            if (target is HeroView heroTarget && heroTarget.IsHighWill && amount > 0)
            {
                amount = Mathf.Max(amount + 1, Mathf.RoundToInt(amount * heroTarget.HighWillBonusMultiplier));
            }
            if (HasBuff(target, BuffType.Frail))
            {
                amount = Mathf.FloorToInt(amount * 0.75f);
            }
            return Mathf.Max(0, amount);
        }

        private IEnumerator ApplyBuffPerformer(ApplyBuffGA applyBuffGA)
        {
            if (applyBuffGA.Targets != null)
            {
                foreach (CombatantView target in applyBuffGA.Targets)
                {
                    if (target == null || target.CurrentHealth <= 0) continue;
                    ApplyBuffInternal(target, applyBuffGA.BuffType, applyBuffGA.Value, applyBuffGA.Duration, applyBuffGA.Caster);
                }
            }
            NotifyStatusEffectsChanged();
            yield return null;
        }

        private IEnumerator ApplyStatusPerformer(ApplyStatusGA applyStatusGA)
        {
            BuffType mappedType = BuffType.Vulnerable;
            if (applyStatusGA.Status == StatusType.Weakness) mappedType = BuffType.Weak;
            else if (applyStatusGA.Status == StatusType.Poison) mappedType = BuffType.Poison;

            if (applyStatusGA.Targets != null)
            {
                foreach (CombatantView target in applyStatusGA.Targets)
                {
                    if (target == null || target.CurrentHealth <= 0) continue;
                    int duration = mappedType == BuffType.Poison
                        ? Mathf.Max(applyStatusGA.Duration, applyStatusGA.Stacks, 1)
                        : Mathf.Max(1, applyStatusGA.Duration);
                    ApplyBuffInternal(target, mappedType, Mathf.Max(1, applyStatusGA.Stacks), duration, applyStatusGA.Caster);
                }
            }
            NotifyStatusEffectsChanged();
            yield return null;
        }

        private void ApplyBuffInternal(CombatantView target, BuffType type, int value, int duration, CombatantView caster)
        {
            if (IsDebuff(type) && TryConsumeArtifact(target))
            {
                return;
            }

            int effectiveValue = value <= 0 && IsStacklessFlag(type) ? 1 : value;
            int effectiveDuration = duration <= 0 ? 999 : duration;

            if (type == BuffType.Poison || type == BuffType.Artifact || type == BuffType.Thorns ||
                type == BuffType.Metallicize || type == BuffType.PlatedArmor || type == BuffType.Barricade)
            {
                BuffData existing = activeBuffs.Find(b => b.Target == target && b.Type == type && b.RemainingTurns > 0);
                if (existing != null)
                {
                    existing.AddValue(Mathf.Max(1, effectiveValue));
                    return;
                }
            }

            BuffData buff = new BuffData(target, type, effectiveValue, effectiveDuration, caster);
            activeBuffs.Add(buff);
        }

        private IEnumerator RemoveDebuffPerformer(RemoveDebuffGA removeDebuffGA)
        {
            if (removeDebuffGA.Targets != null)
            {
                foreach (CombatantView target in removeDebuffGA.Targets)
                {
                    if (target == null) continue;
                    int maxRemove = removeDebuffGA.Count <= 0 ? int.MaxValue : removeDebuffGA.Count;
                    int removedCount = 0;

                    List<BuffData> targetBuffs = GetBuffsFor(target);
                    targetBuffs.Reverse();

                    foreach (BuffData buff in targetBuffs)
                    {
                        if (IsDebuff(buff.Type))
                        {
                            activeBuffs.Remove(buff);
                            removedCount++;
                            if (removedCount >= maxRemove) break;
                        }
                    }

                    if (removedCount < maxRemove && DamageOverTimeSystem.Instance != null)
                    {
                        DamageOverTimeSystem.Instance.RemoveDoTsFor(target, maxRemove - removedCount);
                    }
                }
            }
            NotifyStatusEffectsChanged();
            yield return null;
        }

        public static bool IsDebuff(BuffType type)
        {
            return type == BuffType.Vulnerable || type == BuffType.Weak || type == BuffType.Poison || type == BuffType.Frail;
        }

        private static bool IsStacklessFlag(BuffType type)
        {
            return type == BuffType.FearImmunity || type == BuffType.Redirect || type == BuffType.Barricade;
        }

        private void OnDealDamagePreReaction(DealDamageGA dealDamageGA)
        {
            if (dealDamageGA == null || dealDamageGA.IsThornsDamage) return;

            // Redirect support (future multi-hero & active redirect targets)
            if (dealDamageGA.Targets != null && dealDamageGA.Caster is EnemyView)
            {
                CombatantView redirectHolder = FindRedirectTarget(dealDamageGA.Caster);
                if (redirectHolder != null)
                {
                    for (int i = 0; i < dealDamageGA.Targets.Count; i++)
                    {
                        if (dealDamageGA.Targets[i] is HeroView && dealDamageGA.Targets[i] != redirectHolder)
                        {
                            dealDamageGA.Targets[i] = redirectHolder;
                        }
                    }
                }
            }

            // Outgoing damage modifiers (Caster)
            if (dealDamageGA.Caster != null)
            {
                if (dealDamageGA.Caster is HeroView heroCaster && heroCaster.IsHighWill && dealDamageGA.Amount > 0)
                {
                    dealDamageGA.Amount = Mathf.Max(dealDamageGA.Amount + 1, Mathf.RoundToInt(dealDamageGA.Amount * heroCaster.HighWillBonusMultiplier));
                }

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
            if (dealDamageGA.Targets != null)
            {
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
                            else if (buff.Type == BuffType.Intangible && dealDamageGA.Amount > 1)
                            {
                                dealDamageGA.Amount = 1;
                            }
                        }
                    }
                }
            }

            dealDamageGA.Amount = Mathf.Max(0, dealDamageGA.Amount);
        }

        private void OnGainShieldPreReaction(GainShieldGA gainShieldGA)
        {
            if (gainShieldGA == null || gainShieldGA.Targets == null || gainShieldGA.Targets.Count == 0) return;

            CombatantView reference = gainShieldGA.Caster != null ? gainShieldGA.Caster : gainShieldGA.Targets[0];
            gainShieldGA.Amount = CalculateExpectedShield(gainShieldGA.Amount, reference);
        }

        private void OnEnemyTurnPreReaction(EnemyTurnGA enemyTurnGA)
        {
            // Hero end-of-turn shield buffs (Metallicize, PlatedArmor)
            TriggerEndOfTurnShieldBuffs(isHeroSide: true);

            // Enemy start-of-turn Poison ticks (StS-style: deals stack damage, then decrements by 1)
            TickPoisonForSide(isHeroSide: false);

            DecrementTurnBuffs();
        }

        private void OnEnemyTurnPostReaction(EnemyTurnGA enemyTurnGA)
        {
            // Enemy end-of-turn shield buffs
            TriggerEndOfTurnShieldBuffs(isHeroSide: false);

            // Hero start-of-turn Poison ticks
            TickPoisonForSide(isHeroSide: true);
        }

        private void TriggerEndOfTurnShieldBuffs(bool isHeroSide)
        {
            HashSet<CombatantView> seen = new();
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                BuffData buff = activeBuffs[i];
                if (buff.Target == null || buff.Target.CurrentHealth <= 0 || buff.RemainingTurns <= 0) continue;
                bool targetIsHero = buff.Target is HeroView;
                if (targetIsHero != isHeroSide) continue;
                if (buff.Type != BuffType.Metallicize && buff.Type != BuffType.PlatedArmor) continue;
                if (!seen.Add(buff.Target)) continue;

                int bonusShield = GetBuffStack(buff.Target, BuffType.Metallicize) + GetBuffStack(buff.Target, BuffType.PlatedArmor);
                if (bonusShield > 0)
                {
                    ActionSystem.Instance.AddReaction(new GainShieldGA(bonusShield, new List<CombatantView> { buff.Target }, buff.Target));
                }
            }
        }

        private void TickPoisonForSide(bool isHeroSide)
        {
            bool changed = false;
            List<BuffData> expired = new();

            for (int i = 0; i < activeBuffs.Count; i++)
            {
                BuffData buff = activeBuffs[i];
                if (buff.Type != BuffType.Poison || buff.RemainingTurns <= 0) continue;
                if (buff.Target == null || buff.Target.CurrentHealth <= 0)
                {
                    expired.Add(buff);
                    continue;
                }

                bool targetIsHero = buff.Target is HeroView;
                if (targetIsHero != isHeroSide) continue;

                if (buff.Value > 0)
                {
                    DealDamageGA poisonDamage = new(buff.Value, new List<CombatantView> { buff.Target }, buff.Caster, isThornsDamage: true);
                    ActionSystem.Instance.AddReaction(poisonDamage);
                    buff.DecrementValue(1);
                    changed = true;
                }

                if (buff.Value <= 0)
                {
                    expired.Add(buff);
                }
            }

            foreach (BuffData buff in expired)
            {
                activeBuffs.Remove(buff);
                changed = true;
            }

            if (changed) NotifyStatusEffectsChanged();
        }

        private void DecrementTurnBuffs()
        {
            List<BuffData> expiredBuffs = new();

            foreach (BuffData buff in activeBuffs)
            {
                if (buff.Type == BuffType.Poison)
                {
                    if (buff.Value <= 0 || buff.Target == null || buff.Target.CurrentHealth <= 0)
                        expiredBuffs.Add(buff);
                    continue;
                }

                if (buff.RemainingTurns < 900)
                {
                    buff.DecrementDuration();
                }

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

        private CombatantView FindRedirectTarget(CombatantView attacker)
        {
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                BuffData buff = activeBuffs[i];
                if (buff.Type == BuffType.Redirect && buff.RemainingTurns > 0 &&
                    buff.Target != null && buff.Target != attacker && buff.Target.CurrentHealth > 0)
                {
                    return buff.Target;
                }
            }
            return null;
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
}
