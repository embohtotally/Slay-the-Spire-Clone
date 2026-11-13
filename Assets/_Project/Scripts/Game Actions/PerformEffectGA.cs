using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Actions
{
    public class PerformEffectGA : GameAction
    {
        public Effect Effect { get; set; }
        public List<CombatantView> Targets { get; set; }

        public PerformEffectGA(Effect effect, List<CombatantView> targets)
        {
            Effect = effect;
            Targets = targets == null ? null : new(targets);
        }
    }
}
