using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ApplyBuffEffect")]
    public class ApplyBuffEffect : Effect
    {
        [SerializeField] private BuffType buffType;
        [SerializeField] private int value;
        [SerializeField] private int duration;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            ApplyBuffGA applyBuffGA = new ApplyBuffGA(targets, buffType, value, duration, caster);
            return applyBuffGA;
        }
    }
}
