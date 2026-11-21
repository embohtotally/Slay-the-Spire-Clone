using System.Collections.Generic;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Data;
using UnityEngine;

namespace PlushieOdyssey.Combat.Actions
{
    public class DiscoverCardGA : GameAction
    {
        public int ChoiceCount { get; }
        public bool MakeZeroCostThisTurn { get; }
        public List<CardData> CandidatePool { get; }
        public CombatantView Caster { get; }

        public DiscoverCardGA(int choiceCount, bool makeZeroCostThisTurn, List<CardData> candidatePool = null, CombatantView caster = null)
        {
            ChoiceCount = Mathf.Max(1, choiceCount);
            MakeZeroCostThisTurn = makeZeroCostThisTurn;
            CandidatePool = candidatePool != null ? new List<CardData>(candidatePool) : new List<CardData>();
            Caster = caster;
        }
    }
}

