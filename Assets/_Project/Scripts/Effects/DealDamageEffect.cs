using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "DealDamageEffect")]
    public class DealDamageEffect : Effect
    {
        [SerializeField] private int damageAmount;
        public int DamageAmount => damageAmount;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            DealDamageGA dealDamageGA = new(damageAmount, targets, caster);
            return dealDamageGA;
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            return new DealDamageGA(damageAmount * xMultiplier, targets, caster);
        }
    }
}