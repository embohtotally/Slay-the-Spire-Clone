using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Targeting
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "RandomEnemyTM")]
    public class RandomEnemyTM : TargetMode
    {
        public override List<CombatantView> GetTargets(CombatantView caster = null)
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
}
