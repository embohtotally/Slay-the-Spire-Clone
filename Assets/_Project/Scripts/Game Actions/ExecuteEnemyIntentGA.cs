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
