public class SummonEnemyGA : GameAction, IHaveCaster
{
    public EnemyData EnemyData { get; private set; }
    public int Count { get; private set; }
    public CombatantView Caster { get; private set; }

    public SummonEnemyGA(EnemyData enemyData, int count, CombatantView caster)
    {
        EnemyData = enemyData;
        Count = count;
        Caster = caster;
    }
}
