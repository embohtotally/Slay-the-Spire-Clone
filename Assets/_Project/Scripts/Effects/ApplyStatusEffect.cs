using System.Collections.Generic;
using UnityEngine;

public class ApplyStatusEffect : Effect
{
    [SerializeField] private StatusType status;
    [SerializeField] private int stacks;
    [SerializeField] private int duration;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return new ApplyStatusGA(targets, status, stacks, duration, caster);
    }
}
