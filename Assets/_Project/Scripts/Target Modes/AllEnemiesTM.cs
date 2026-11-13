using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Targeting
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "AllEnemiesTM")]
    public class AllEnemiesTM : TargetMode
    {
        public override List<CombatantView> GetTargets(CombatantView caster = null)
        {
            return new(EnemySystem.Instance.Enemies);
        }
    }
}