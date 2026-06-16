using System.Collections.Generic;
using UnityEngine;

public class GainShieldGA : GameAction, IHaveCaster
{
    public List<CombatantView> Targets { get; set; }
    public int Amount { get; set; }

    public CombatantView Caster { get; private set; }

    public GainShieldGA(int amount, List<CombatantView> targets, CombatantView caster)
    {
        Amount = amount;
        Targets = new List<CombatantView>(targets);
        Caster = caster;
    }
}
