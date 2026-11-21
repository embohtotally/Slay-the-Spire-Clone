using System.Collections.Generic;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    public class ScryEffect : Effect
    {
        [SerializeField, Min(1)] private int amount = 3;

        public int Amount => Mathf.Max(1, amount);

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return new ScryGA(Amount, caster);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            return new ScryGA(Amount * xMultiplier, caster);
        }
    }
}

