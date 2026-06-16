using System.Collections.Generic;
using UnityEngine;

public class ApplyStunEffect : Effect
{
    [SerializeField] private int duration;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        ApplyStunGA applyStunGA = new ApplyStunGA(duration, targets, caster);
        return applyStunGA;
    }
}
