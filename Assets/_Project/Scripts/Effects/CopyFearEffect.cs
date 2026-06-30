using System.Collections.Generic;
using UnityEngine;

public class CopyFearEffect : Effect
{
    [SerializeField] private string sourceAllyName;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return new SetStressGA(sourceAllyName, targets, caster);
    }
}
