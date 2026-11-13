using PlushieOdyssey.Combat;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Actions
{
    public class ApplyBuffGA : GameAction, IHaveCaster
    {
        public List<CombatantView> Targets { get; private set; }
        public BuffType BuffType { get; private set; }
        public int Value { get; private set; }
        public int Duration { get; private set; }
        public CombatantView Caster { get; private set; }

        public ApplyBuffGA(List<CombatantView> targets, BuffType buffType, int value, int duration, CombatantView caster)
        {
            Targets = targets;
            BuffType = buffType;
            Value = value;
            Duration = duration;
            Caster = caster;
        }
    }
}
