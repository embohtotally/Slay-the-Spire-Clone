using PlushieOdyssey.Combat;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Actions
{
    public class SimultaneousGA : GameAction, IHaveCaster
    {
        public List<GameAction> Actions { get; private set; }
        public CombatantView Caster { get; private set; }

        public SimultaneousGA(List<GameAction> actions, CombatantView caster)
        {
            Actions = actions;
            Caster = caster;
        }
    }
}
