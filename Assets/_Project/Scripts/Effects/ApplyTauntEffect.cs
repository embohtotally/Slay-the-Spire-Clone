using System.Collections.Generic;
using UnityEngine;

public class ApplyTauntEffect : Effect
{
    [SerializeField] private int duration;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        ApplyTauntGA applyTauntGA = new ApplyTauntGA(duration, targets, caster);
        return applyTauntGA;
    }
}
