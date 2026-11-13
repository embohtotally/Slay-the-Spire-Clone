using PlushieOdyssey.Combat;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
namespace PlushieOdyssey.Combat.Actions
{
    public class ExecuteEnemyIntentGA : GameAction, IHaveCaster
    {
        public EnemyView Attacker { get; private set; }
        public CombatantView Caster => Attacker;
        public EnemyIntent Intent { get; private set; }

        public ExecuteEnemyIntentGA(EnemyView attacker, EnemyIntent intent)
        {
            Attacker = attacker;
            Intent = intent;
        }
    }
}
