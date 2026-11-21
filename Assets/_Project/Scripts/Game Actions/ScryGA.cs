using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Actions
{
    public class ScryGA : GameAction
    {
        public int Amount { get; }
        public CombatantView Caster { get; }

        public ScryGA(int amount, CombatantView caster = null)
        {
            Amount = Mathf.Max(0, amount);
            Caster = caster;
        }
    }
}

