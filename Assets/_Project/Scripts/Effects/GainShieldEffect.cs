using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "GainShieldEffect")]
    public class GainShieldEffect : Effect
    {
        [SerializeField] private int shieldAmount;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            GainShieldGA gainShieldGA = new GainShieldGA(shieldAmount, targets, caster);
            return gainShieldGA;
        }
    }
}
