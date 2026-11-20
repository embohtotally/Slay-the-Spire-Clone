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
    public class DamageOverTimeSystem : Singleton<DamageOverTimeSystem>
    {
        private readonly List<DamageOverTimeData> activeDoTs = new();

        public event Action StatusEffectsChanged;

        public IReadOnlyList<DamageOverTimeData> ActiveDoTs => activeDoTs;

        private void OnEnable()
        {
            ActionSystem.AttachPerformer<ApplyDoTGA>(ApplyDoTPerformer);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPreReaction, ReactionTiming.PRE);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPostReaction, ReactionTiming.POST);
        }

        private void OnDisable()
        {
            ActionSystem.DetachPerformer<ApplyDoTGA>();
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPreReaction, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPostReaction, ReactionTiming.POST);
        }

        public int RemoveDoTsFor(CombatantView target, int maxCount = int.MaxValue)
        {
            if (target == null || maxCount <= 0) return 0;

            int removed = 0;
            for (int i = activeDoTs.Count - 1; i >= 0 && removed < maxCount; i--)
            {
                if (activeDoTs[i].Target == target)
                {
                    activeDoTs.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0) NotifyStatusEffectsChanged();
            return removed;
        }

        private IEnumerator ApplyDoTPerformer(ApplyDoTGA applyDoTGA)
        {
            if (applyDoTGA.Targets != null)
            {
                foreach (CombatantView target in applyDoTGA.Targets)
                {
                    if (target == null || target.CurrentHealth <= 0) continue;
                    if (BuffSystem.Instance != null && BuffSystem.Instance.TryConsumeArtifact(target))
                        continue;

                    DamageOverTimeData dot = new DamageOverTimeData(target, applyDoTGA.DamagePerTurn, applyDoTGA.Duration, applyDoTGA.Caster);
                    activeDoTs.Add(dot);
                }
            }
            NotifyStatusEffectsChanged();
            yield return null;
        }

        private void OnEnemyTurnPreReaction(EnemyTurnGA enemyTurnGA)
        {
            // Enemy DoTs tick at start of Enemy turn
            TickDoTsForSide(isHeroSide: false);
        }

        private void OnEnemyTurnPostReaction(EnemyTurnGA enemyTurnGA)
        {
            // Hero DoTs tick at start of Player turn
            TickDoTsForSide(isHeroSide: true);
        }

        private void TickDoTsForSide(bool isHeroSide)
        {
            List<DamageOverTimeData> expiredDoTs = new();
            bool changed = false;

            foreach (DamageOverTimeData dot in activeDoTs)
            {
                if (dot.Target == null || dot.Target.CurrentHealth <= 0)
                {
                    expiredDoTs.Add(dot);
                    continue;
                }

                bool targetIsHero = dot.Target is HeroView;
                if (targetIsHero != isHeroSide) continue;

                DealDamageGA dealDamageGA = new(dot.DamagePerTurn, new List<CombatantView> { dot.Target }, dot.Caster, isThornsDamage: true);
                ActionSystem.Instance.AddReaction(dealDamageGA);

                dot.DecrementDuration();
                changed = true;
                if (dot.RemainingTurns <= 0)
                {
                    expiredDoTs.Add(dot);
                }
            }

            foreach (DamageOverTimeData dot in expiredDoTs)
            {
                activeDoTs.Remove(dot);
                changed = true;
            }

            if (changed)
            {
                NotifyStatusEffectsChanged();
            }
        }

        public List<DamageOverTimeData> GetDoTsFor(CombatantView target)
        {
            List<DamageOverTimeData> results = new();
            if (target == null) return results;

            foreach (DamageOverTimeData dot in activeDoTs)
            {
                if (dot.Target == target && dot.RemainingTurns > 0)
                {
                    results.Add(dot);
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
