using System.Collections.Generic;
using UnityEngine;

public class SimultaneousEffect : Effect
{
    [field: SerializeField] public List<AutoTargetEffect> Effects { get; private set; }

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        List<GameAction> childActions = new List<GameAction>();
        if (Effects != null)
        {
            foreach (AutoTargetEffect effect in Effects)
            {
                if (effect.Effect != null)
                {
                    List<CombatantView> specificTargets = effect.TargetMode != null ? effect.TargetMode.GetTargets(caster) : null;
                    childActions.Add(effect.Effect.GetGameAction(specificTargets, caster));
                }
            }
        }

        SimultaneousGA simultaneousGA = new SimultaneousGA(childActions, caster);
        return simultaneousGA;
    }
}
