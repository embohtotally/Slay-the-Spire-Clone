using System.Collections.Generic;
using UnityEngine;

public class CostReductionEffect : Effect
{
    [SerializeField] private string targetCardName;
    [SerializeField] private int reduction;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return new ApplyCostModifierGA(targetCardName, reduction, caster);
    }
}
