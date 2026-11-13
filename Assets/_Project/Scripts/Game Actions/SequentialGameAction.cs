using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Actions
{
    public class SequentialGameAction : GameAction
    {
        public List<GameAction> Actions { get; private set; }

        public SequentialGameAction(List<GameAction> actions)
        {
            Actions = actions;
        }
    }
}
