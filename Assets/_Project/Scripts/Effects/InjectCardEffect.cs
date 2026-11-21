using System.Collections.Generic;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Data;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    public class InjectCardEffect : Effect
    {
        [SerializeField] private CardData cardToInject;
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField] private CardPileDestination destination = CardPileDestination.DiscardPile;

        public CardData CardToInject => cardToInject;
        public int Count => Mathf.Max(1, count);
        public CardPileDestination Destination => destination;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            if (cardToInject == null) return null;
            return new InjectCardGA(cardToInject, Count, destination, caster);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0 || cardToInject == null) return null;
            return new InjectCardGA(cardToInject, Count * xMultiplier, destination, caster);
        }
    }
}

