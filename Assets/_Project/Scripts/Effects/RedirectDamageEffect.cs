using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "RedirectDamageEffect")]
    public class RedirectDamageEffect : Effect
    {
        [SerializeField] private int duration;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return new ApplyBuffGA(targets, BuffType.Redirect, 1, duration, caster);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            return new ApplyBuffGA(targets, BuffType.Redirect, 1, duration * xMultiplier, caster);
        }
    }
}
