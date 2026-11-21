using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Actions
{
    public enum HandChoiceMode
    {
        Discard = 0,
        Exhaust = 1,
        UpgradeInCombat = 2
    }

    public class HandChoiceGA : GameAction
    {
        public HandChoiceMode Mode { get; }
        public int Count { get; }
        public CombatantView Caster { get; }

        public HandChoiceGA(HandChoiceMode mode, int count, CombatantView caster = null)
        {
            Mode = mode;
            Count = Mathf.Max(1, count);
            Caster = caster;
        }
    }
}

