using System.Collections.Generic;
using UnityEngine;

public class GainShieldEffect : Effect
{
    [SerializeField] private int shieldAmount;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        GainShieldGA gainShieldGA = new GainShieldGA(shieldAmount, targets, caster);
        return gainShieldGA;
    }
}
