using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "SimultaneousEffect")]
    public class SimultaneousEffect : Effect
    {
        [field: SerializeField] public List<AutoTargetEffect> Effects { get; private set; }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return GetGameAction(targets, caster, 1);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;

            List<GameAction> childActions = new List<GameAction>();
            if (Effects != null)
            {
                foreach (AutoTargetEffect effect in Effects)
                {
                    if (effect != null && effect.Effect != null)
                    {
                        List<CombatantView> specificTargets = effect.TargetMode != null ? effect.TargetMode.GetTargets(caster) : targets;
                        GameAction action = effect.Effect.GetGameAction(specificTargets, caster, xMultiplier);
                        if (action != null) childActions.Add(action);
                    }
                }
            }

            if (childActions.Count == 0) return null;
            return new SimultaneousGA(childActions, caster);
        }
    }
}
