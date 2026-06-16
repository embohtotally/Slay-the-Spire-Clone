using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayCardGA : GameAction
{
    public CombatantView ManualTarget { get; private set; }
    public Card Card { get; set; }

    public PlayCardGA(Card card)
    {
        Card = card;
        ManualTarget = null;
    }

    public PlayCardGA(Card card, CombatantView target)
    {
        Card = card;
        ManualTarget = target;
    }
}