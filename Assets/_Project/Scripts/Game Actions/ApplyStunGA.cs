using System.Collections.Generic;
using UnityEngine;

public class ApplyStunGA : GameAction, IHaveCaster
{
    public List<CombatantView> Targets { get; set; }
    public int Duration { get; set; }

    public CombatantView Caster { get; private set; }

    public ApplyStunGA(int duration, List<CombatantView> targets, CombatantView caster)
    {
        Duration = duration;
        Targets = new List<CombatantView>(targets);
        Caster = caster;
    }
}
