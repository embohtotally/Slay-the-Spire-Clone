using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "AddStressEffect")]
    public class AddStressEffect : Effect
    {
        [SerializeField] private int stressAmount;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            AddStressGA addStressGA = new AddStressGA(stressAmount, targets, caster);
            return addStressGA;
        }
    }
}
