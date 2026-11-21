using System.Collections.Generic;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    public class SpendWillEffect : Effect
    {
        [SerializeField, Min(0)] private int willCost = 15;

        public int WillCost => Mathf.Max(0, willCost);

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            if (WillCost <= 0) return null;
            return new ModifyStressGA(WillCost, targets, caster, ignoreImmunity: true);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0 || WillCost <= 0) return null;
            return new ModifyStressGA(WillCost * xMultiplier, targets, caster, ignoreImmunity: true);
        }
    }
}

