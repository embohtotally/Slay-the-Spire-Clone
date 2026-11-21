using System.Collections.Generic;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    public class HandChoiceEffect : Effect
    {
        [SerializeField] private HandChoiceMode mode = HandChoiceMode.Discard;
        [SerializeField, Min(1)] private int count = 1;

        public HandChoiceMode Mode => mode;
        public int Count => Mathf.Max(1, count);

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return new HandChoiceGA(mode, Count, caster);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            return new HandChoiceGA(mode, Count * xMultiplier, caster);
        }
    }
}

