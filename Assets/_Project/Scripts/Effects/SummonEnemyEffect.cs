using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Data;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "SummonEnemyEffect")]
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
}
