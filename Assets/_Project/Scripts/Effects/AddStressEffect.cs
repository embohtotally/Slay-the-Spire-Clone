using System.Collections.Generic;
using UnityEngine;

public class AddStressEffect : Effect
{
    [SerializeField] private int stressAmount;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        AddStressGA addStressGA = new AddStressGA(stressAmount, targets, caster);
        return addStressGA;
    }
}
