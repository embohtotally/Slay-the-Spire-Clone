using System.Collections.Generic;
using UnityEngine;

public class GainAPEffect : Effect
{
    [SerializeField] private int amount;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return new ModifyManaGA(amount, caster);
    }
}
