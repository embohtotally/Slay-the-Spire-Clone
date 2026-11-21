using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Data;
using UnityEngine;

namespace PlushieOdyssey.Combat.Actions
{
    public enum CardPileDestination
    {
        DiscardPile = 0,
        DrawPileRandom = 1,
        DrawPileTop = 2,
        Hand = 3
    }

    public class InjectCardGA : GameAction
    {
        public CardData CardData { get; }
        public int Count { get; }
        public CardPileDestination Destination { get; }
        public CombatantView Caster { get; }

        public InjectCardGA(CardData cardData, int count, CardPileDestination destination = CardPileDestination.DiscardPile, CombatantView caster = null)
        {
            CardData = cardData;
            Count = Mathf.Max(1, count);
            Destination = destination;
            Caster = caster;
        }
    }
}

