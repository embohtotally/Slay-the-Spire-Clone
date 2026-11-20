using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Actions
{
    public class PlayCardGA : GameAction
    {
        public CombatantView ManualTarget { get; private set; }
        public Card Card { get; set; }
        public int ManaSpent { get; private set; }
        public bool HasManaSpentOverride { get; private set; }

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

        public PlayCardGA(Card card, CombatantView target, int manaSpentOverride)
        {
            Card = card;
            ManualTarget = target;
            ManaSpent = Mathf.Max(0, manaSpentOverride);
            HasManaSpentOverride = true;
        }
    }
}
