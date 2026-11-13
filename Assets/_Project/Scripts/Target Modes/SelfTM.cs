using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Targeting
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "SelfTM")]
    public class SelfTM : TargetMode
    {
        public override List<CombatantView> GetTargets(CombatantView caster = null)
        {
            if (caster == null) return new List<CombatantView>();
            return new List<CombatantView>() { caster };
        }
    }
}
