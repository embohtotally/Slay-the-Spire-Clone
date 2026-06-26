using System.Collections.Generic;
using UnityEngine;

public class ApplyBuffEffect : Effect
{
    [SerializeField] private BuffType buffType;
    [SerializeField] private int value;
    [SerializeField] private int duration;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        ApplyBuffGA applyBuffGA = new ApplyBuffGA(targets, buffType, value, duration, caster);
        return applyBuffGA;
    }
}
