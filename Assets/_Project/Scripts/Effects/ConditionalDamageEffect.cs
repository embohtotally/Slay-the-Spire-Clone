using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ConditionalDamageEffect")]
    public class ConditionalDamageEffect : Effect
    {
        [SerializeField] private int baseDamage;
        [SerializeField] private int bonusDamage;
        [SerializeField] private ConditionType condition;
        [SerializeField] private int threshold;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            int totalDamage = baseDamage;
            bool conditionMet = false;

            switch (condition)
            {
                case ConditionType.CasterFearAtLeast:
                    if (caster is HeroView heroView)
                    {
                        if (heroView.CurrentStress >= threshold) conditionMet = true;
                    }
                    break;
                case ConditionType.TargetHasStatus:
                    break;
            }

            if (conditionMet)
            {
                totalDamage += bonusDamage;
            }

            return new DealDamageGA(totalDamage, targets, caster);
        }
    }
}
