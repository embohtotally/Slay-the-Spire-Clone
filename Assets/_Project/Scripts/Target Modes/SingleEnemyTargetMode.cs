using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SingleEnemyTargetMode : TargetMode
{
    public override List<CombatantView> GetTargets(CombatantView caster = null)
    {
        List<CombatantView> targets = new();
        if (EnemySystem.Instance != null && EnemySystem.Instance.Enemies.Count > 0)
        {
            var enemies = EnemySystem.Instance.Enemies;
            targets.Add(enemies[Random.Range(0, enemies.Count)]);
        }
        return targets;
    }
}
