using System.Collections.Generic;
using UnityEngine;

public class RedirectDamageEffect : Effect
{
    [SerializeField] private int duration;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return new ApplyBuffGA(targets, BuffType.Redirect, 1, duration, caster);
    }
}
