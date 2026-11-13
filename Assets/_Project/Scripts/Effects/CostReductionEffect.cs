using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "CostReductionEffect")]
    public class CostReductionEffect : Effect
    {
        [SerializeField] private string targetCardName;
        [SerializeField] private int reduction;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return new ApplyCostModifierGA(targetCardName, reduction, caster);
        }
    }
}
