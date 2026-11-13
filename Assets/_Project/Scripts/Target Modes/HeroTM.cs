using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Targeting
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "HeroTM")]
    public class HeroTM : TargetMode
    {
        public override List<CombatantView> GetTargets(CombatantView caster = null)
        {
            return new List<CombatantView>() { HeroSystem.Instance.HeroView };
        }
    }
}
