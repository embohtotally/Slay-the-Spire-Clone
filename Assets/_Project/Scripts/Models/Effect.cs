using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Models
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "Effect")]
    [System.Serializable]
    public abstract class Effect
    {
        public abstract GameAction GetGameAction(List<CombatantView> targets, CombatantView caster);
    }
}