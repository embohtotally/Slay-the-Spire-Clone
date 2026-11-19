using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
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

        public int BaseDamage => baseDamage;
        public int BonusDamage => bonusDamage;

        public int EvaluateDamage(List<CombatantView> targets, CombatantView caster)
        {
            int totalDamage = baseDamage;
            bool conditionMet = false;

            switch (condition)
            {
                case ConditionType.CasterFearAtLeast:
                    if (caster is HeroView heroView && heroView.CurrentStress >= threshold)
                    {
                        conditionMet = true;
                    }
                    break;
                case ConditionType.TargetHasStatus:
                    if (targets != null)
                    {
                        foreach (CombatantView target in targets)
                        {
                            if (HasAnyActiveStatus(target))
                            {
                                conditionMet = true;
                                break;
                            }
                        }
                    }
                    break;
            }

            if (conditionMet) totalDamage += bonusDamage;
            return totalDamage;
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return new DealDamageGA(EvaluateDamage(targets, caster), targets, caster);
        }

        private static bool HasAnyActiveStatus(CombatantView target)
        {
            if (target == null) return false;
            if (target.IsStunned || target.IsTaunted) return true;
            if (BuffSystem.Instance != null && BuffSystem.Instance.GetBuffsFor(target).Count > 0) return true;
            if (DamageOverTimeSystem.Instance != null && DamageOverTimeSystem.Instance.GetDoTsFor(target).Count > 0) return true;
            return false;
        }
    }
}
