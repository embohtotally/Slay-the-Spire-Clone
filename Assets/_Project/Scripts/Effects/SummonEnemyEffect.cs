using System.Collections.Generic;
using UnityEngine;

public class SummonEnemyEffect : Effect
{
    [SerializeField] private EnemyData enemyToSummon;
    [SerializeField] private int summonCount = 1;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        SummonEnemyGA summonEnemyGA = new SummonEnemyGA(enemyToSummon, summonCount, caster);
        return summonEnemyGA;
    }
}
