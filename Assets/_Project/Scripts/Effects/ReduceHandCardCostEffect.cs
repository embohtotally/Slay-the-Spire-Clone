using System.Collections.Generic;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    public class ReduceHandCardCostEffect : Effect
    {
        [SerializeField, Min(1)] private int reduction = 1;
        [SerializeField] private HandCostReductionTarget targetMode = HandCostReductionTarget.RandomInHand;
        [SerializeField] private bool thisTurnOnly = true;

        public int Reduction => Mathf.Max(1, reduction);
        public HandCostReductionTarget TargetMode => targetMode;
        public bool ThisTurnOnly => thisTurnOnly;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return new ReduceHandCardCostGA(Reduction, targetMode, thisTurnOnly, caster);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            return new ReduceHandCardCostGA(Reduction * xMultiplier, targetMode, thisTurnOnly, caster);
        }
    }
}

