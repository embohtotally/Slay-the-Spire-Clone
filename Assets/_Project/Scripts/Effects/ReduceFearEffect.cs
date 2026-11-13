using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ReduceFearEffect")]
    public class ReduceFearEffect : Effect
    {
        [SerializeField] private int amount;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return new ModifyStressGA(-amount, targets, caster);
        }
    }
}
