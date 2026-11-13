using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Models
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "TargetMode")]
    [System.Serializable]
    public abstract class TargetMode
    {
        public abstract List<CombatantView> GetTargets(CombatantView caster = null);
    }
}