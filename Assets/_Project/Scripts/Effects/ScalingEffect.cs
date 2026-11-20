using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Run;
using System.Collections.Generic;
using SerializeReferenceEditor;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ScalingEffect")]
    public class ScalingEffect : Effect
    {
        [SerializeReference, SR] private Effect baseEffect;
        [SerializeReference, SR] private Effect scalingEffect;
        [SerializeField] private int step;
        [SerializeField] private string counterKey;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return GetGameAction(targets, caster, 1);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;

            int count = 0;
            if (RunManager.Instance != null && RunManager.Instance.Counters.ContainsKey(counterKey))
            {
                count = RunManager.Instance.Counters[counterKey];
            }

            List<GameAction> actions = new();

            if (baseEffect != null)
            {
                GameAction baseAction = baseEffect.GetGameAction(targets, caster, xMultiplier);
                if (baseAction != null) actions.Add(baseAction);
            }

            if (scalingEffect != null && count > 0)
            {
                GameAction scaledAction = scalingEffect.GetGameAction(targets, caster, count * xMultiplier);
                if (scaledAction != null) actions.Add(scaledAction);
            }

            if (RunManager.Instance != null)
            {
                if (!RunManager.Instance.Counters.ContainsKey(counterKey))
                {
                    RunManager.Instance.Counters[counterKey] = 0;
                }
                RunManager.Instance.Counters[counterKey] += step;
            }

            return actions.Count > 0 ? new SequentialGameAction(actions) : null;
        }
    }
}
