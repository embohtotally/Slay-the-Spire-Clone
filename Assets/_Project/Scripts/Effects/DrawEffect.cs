using System.Collections.Generic;
using UnityEngine;

public class DrawEffect : Effect
{
    [SerializeField] private int amount;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return new DrawCardsGA(amount);
    }
}
