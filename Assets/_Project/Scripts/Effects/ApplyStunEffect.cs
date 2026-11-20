using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ApplyStunEffect")]
    public class ApplyStunEffect : Effect
    {
        [SerializeField] private int duration;

        public int Duration => duration;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            ApplyStunGA applyStunGA = new ApplyStunGA(duration, targets, caster);
            return applyStunGA;
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            return new ApplyStunGA(duration * xMultiplier, targets, caster);
        }
    }
}
