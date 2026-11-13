using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Data;
using System.Collections.Generic;

namespace PlushieOdyssey.Relics
{
    public sealed class RelicTriggerContext
    {
        public RelicData Relic { get; }
        public RelicTriggerType TriggerType { get; }
        public ReactionTiming Timing { get; }
        public GameAction SourceAction { get; }
        public CombatantView Caster { get; }
        public IReadOnlyList<CombatantView> Targets { get; }
        public Card PlayedCard { get; }

        public RelicTriggerContext(
            RelicData relic,
            RelicTriggerType triggerType,
            ReactionTiming timing,
            GameAction sourceAction,
            CombatantView caster,
            IReadOnlyList<CombatantView> targets,
            Card playedCard)
        {
            Relic = relic;
            TriggerType = triggerType;
            Timing = timing;
            SourceAction = sourceAction;
            Caster = caster;
            Targets = targets;
            PlayedCard = playedCard;
        }
    }
}
