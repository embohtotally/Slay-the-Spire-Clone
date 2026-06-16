using System.Collections.Generic;
using UnityEngine;

public class ApplyTauntGA : GameAction, IHaveCaster
{
    public List<CombatantView> Targets { get; set; }
    public int Duration { get; set; }

    public CombatantView Caster { get; private set; }

    public ApplyTauntGA(int duration, List<CombatantView> targets, CombatantView caster)
    {
        Duration = duration;
        Targets = new List<CombatantView>(targets);
        Caster = caster;
    }
}
