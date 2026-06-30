using System.Collections.Generic;
using UnityEngine;

public class ReduceFearEffect : Effect
{
    [SerializeField] private int amount;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return new ModifyStressGA(-amount, targets, caster);
    }
}
