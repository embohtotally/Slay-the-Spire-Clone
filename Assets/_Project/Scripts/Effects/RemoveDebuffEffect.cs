using System.Collections.Generic;
using UnityEngine;

public class RemoveDebuffEffect : Effect
{
    [SerializeField] private int count;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return new RemoveDebuffGA(count, targets, caster);
    }
}
