using PlushieOdyssey.Combat;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Actions
{
    public class AddStressGA : GameAction, IHaveCaster
    {
        public List<CombatantView> Targets { get; private set; }
        public int Amount { get; private set; }
        public CombatantView Caster { get; private set; }

        public AddStressGA(int amount, List<CombatantView> targets, CombatantView caster)
        {
            Amount = amount;
            Targets = targets;
            Caster = caster;
        }
    }
}
