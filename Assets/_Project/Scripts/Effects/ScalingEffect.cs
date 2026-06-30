using System.Collections.Generic;
using SerializeReferenceEditor;
using UnityEngine;

public class ScalingEffect : Effect
{
    [SerializeReference, SR] private Effect baseEffect;
    [SerializeReference, SR] private Effect scalingEffect;
    [SerializeField] private int step;
    [SerializeField] private string counterKey;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        int count = 0;
        if (RunManager.Instance != null && RunManager.Instance.Counters.ContainsKey(counterKey))
        {
            count = RunManager.Instance.Counters[counterKey];
        }

        List<GameAction> actions = new();

        if (baseEffect != null)
        {
            actions.Add(baseEffect.GetGameAction(targets, caster));
        }

        if (scalingEffect != null && count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                actions.Add(scalingEffect.GetGameAction(targets, caster));
            }
        }

        if (RunManager.Instance != null)
        {
            if (!RunManager.Instance.Counters.ContainsKey(counterKey))
            {
                RunManager.Instance.Counters[counterKey] = 0;
            }
            RunManager.Instance.Counters[counterKey] += step;
        }

        return new SequentialGameAction(actions);
    }
}
