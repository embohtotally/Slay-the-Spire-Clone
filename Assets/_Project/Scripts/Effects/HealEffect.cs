using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "HealEffect")]
    public class HealEffect : Effect
    {
        [SerializeField] private int healAmount;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            HealGA healGA = new HealGA(healAmount, targets, caster);
            return healGA;
        }
    }
}
