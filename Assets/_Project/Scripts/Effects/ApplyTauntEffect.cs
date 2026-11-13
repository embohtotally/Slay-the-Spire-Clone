using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ApplyTauntEffect")]
    public class ApplyTauntEffect : Effect
    {
        [SerializeField] private int duration;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            ApplyTauntGA applyTauntGA = new ApplyTauntGA(duration, targets, caster);
            return applyTauntGA;
        }
    }
}
