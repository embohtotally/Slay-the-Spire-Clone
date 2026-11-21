using System.Collections.Generic;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    public enum ComboScalingSource
    {
        CardsPlayedThisTurn = 0,
        TurnsRetained = 1,
        AttacksPlayedThisTurn = 2,
        HandSize = 3
    }

    public class ComboDamageEffect : Effect
    {
        [SerializeField, Min(0)] private int baseDamage = 6;
        [SerializeField] private int bonusPerStack = 4;
        [SerializeField] private ComboScalingSource scalingSource = ComboScalingSource.CardsPlayedThisTurn;

        public int BaseDamage => baseDamage;
        public int BonusPerStack => bonusPerStack;
        public ComboScalingSource ScalingSource => scalingSource;

        public int EvaluateDamage(Card sourceCard = null)
        {
            int stacks = GetCurrentStacks(sourceCard);
            return Mathf.Max(0, baseDamage + bonusPerStack * stacks);
        }

        public int GetCurrentStacks(Card sourceCard = null)
        {
            CardSystem cs = CardSystem.Instance;
            return scalingSource switch
            {
                ComboScalingSource.CardsPlayedThisTurn => cs != null ? cs.CardsPlayedThisTurn : 0,
                ComboScalingSource.TurnsRetained => sourceCard != null
                    ? sourceCard.TurnsRetained
                    : (cs != null && cs.LastPlayedCard != null ? cs.LastPlayedCard.TurnsRetained : 0),
                ComboScalingSource.AttacksPlayedThisTurn => cs != null ? cs.AttacksPlayedThisTurn : 0,
                ComboScalingSource.HandSize => cs != null ? cs.Hand.Count : 0,
                _ => 0
            };
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            int dmg = EvaluateDamage(CardSystem.Instance != null ? CardSystem.Instance.LastPlayedCard : null);
            return new DealDamageGA(dmg, targets, caster);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            int dmg = EvaluateDamage(CardSystem.Instance != null ? CardSystem.Instance.LastPlayedCard : null) * xMultiplier;
            return new DealDamageGA(dmg, targets, caster);
        }
    }
}

