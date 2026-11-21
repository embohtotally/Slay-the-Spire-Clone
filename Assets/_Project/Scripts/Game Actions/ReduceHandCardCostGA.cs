using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Actions
{
    public enum HandCostReductionTarget
    {
        RandomInHand = 0,
        AllInHand = 1,
        HighestCostInHand = 2
    }

    public class ReduceHandCardCostGA : GameAction
    {
        public int Reduction { get; }
        public HandCostReductionTarget TargetMode { get; }
        public bool ThisTurnOnly { get; }
        public CombatantView Caster { get; }

        public ReduceHandCardCostGA(int reduction, HandCostReductionTarget targetMode = HandCostReductionTarget.RandomInHand, bool thisTurnOnly = true, CombatantView caster = null)
        {
            Reduction = Mathf.Max(1, reduction);
            TargetMode = targetMode;
            ThisTurnOnly = thisTurnOnly;
            Caster = caster;
        }
    }
}

