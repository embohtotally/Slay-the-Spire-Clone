using PlushieOdyssey.Combat;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Actions
{
    public class HealGA : GameAction, IHaveCaster
    {
        public List<CombatantView> Targets { get; set; }
        public int Amount { get; set; }

        public CombatantView Caster { get; private set; }

        public HealGA(int amount, List<CombatantView> targets, CombatantView caster)
        {
            Amount = amount;
            Targets = new List<CombatantView>(targets);
            Caster = caster;
        }
    }
}
