using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ApplyStatusEffect")]
    public class ApplyStatusEffect : Effect
    {
        [SerializeField] private StatusType status;
        [SerializeField] private int stacks;
        [SerializeField] private int duration;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return new ApplyStatusGA(targets, status, stacks, duration, caster);
        }
    }
}
