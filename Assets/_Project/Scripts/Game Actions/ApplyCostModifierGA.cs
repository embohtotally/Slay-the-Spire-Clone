using PlushieOdyssey.Combat;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Actions
{
    public class ApplyCostModifierGA : GameAction, IHaveCaster
    {
        public string TargetCardName { get; private set; }
        public int ReductionAmount { get; private set; }
        public CombatantView Caster { get; private set; }

        public ApplyCostModifierGA(string targetCardName, int reductionAmount, CombatantView caster)
        {
            TargetCardName = targetCardName;
            ReductionAmount = reductionAmount;
            Caster = caster;
        }
    }
}
