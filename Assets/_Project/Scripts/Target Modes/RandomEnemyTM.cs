using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomEnemyTM : TargetMode
{
    public override List<CombatantView> GetTargets()
    {
        List<EnemyView> validEnemies = EnemySystem.Instance.Enemies;
        
        bool anyTaunt = validEnemies.Exists(e => e.IsTaunted);
        if (anyTaunt)
        {
            validEnemies = validEnemies.FindAll(e => e.IsTaunted);
        }

        CombatantView target = validEnemies[UnityEngine.Random.Range(0, validEnemies.Count)];
        return new() { target };
    }
}