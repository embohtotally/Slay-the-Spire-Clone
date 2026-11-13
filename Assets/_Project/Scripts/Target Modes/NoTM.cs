using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Targeting
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "NoTM")]
    public class NoTM : TargetMode
    {
        public override List<CombatantView> GetTargets(CombatantView caster = null)
        {
            return null;
        }
    }
}