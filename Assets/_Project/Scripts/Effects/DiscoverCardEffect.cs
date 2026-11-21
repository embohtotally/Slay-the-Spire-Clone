using System.Collections.Generic;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Data;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    public class DiscoverCardEffect : Effect
    {
        [SerializeField, Min(1)] private int choiceCount = 3;
        [SerializeField] private bool makeZeroCostThisTurn = true;
        [SerializeField] private List<CardData> candidatePool = new();

        public int ChoiceCount => Mathf.Max(1, choiceCount);
        public bool MakeZeroCostThisTurn => makeZeroCostThisTurn;
        public IReadOnlyList<CardData> CandidatePool => candidatePool;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return new DiscoverCardGA(ChoiceCount, makeZeroCostThisTurn, candidatePool, caster);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            if (xMultiplier == 1) return GetGameAction(targets, caster);

            List<GameAction> list = new(xMultiplier);
            for (int i = 0; i < xMultiplier; i++)
                list.Add(GetGameAction(targets, caster));
            return new SequentialGameAction(list);
        }
    }
}

