using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Actions
{
    public abstract class GameAction
    {
        public List<GameAction> PreReactions { get; private set; } = new();
        public List<GameAction> PerformReactions { get; private set; } = new();
        public List<GameAction> PostReactions { get; private set; } = new();
    }
}
