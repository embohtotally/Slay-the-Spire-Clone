using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Models
{
    /// <summary>
    /// Controls which parameter of a multi-field effect (Buff, Status, DoT) multiplies by X on X-cost cards.
    /// </summary>
    public enum XScalingAxis
    {
        /// <summary>Uses the natural stacking rule for the effect (e.g. Poison/Strength scale stacks/value; Vulnerable/Weak/Frail scale duration).</summary>
        Auto = 0,
        /// <summary>Multiplies magnitude / stacks / damage-per-turn by X while keeping duration fixed.</summary>
        Value = 1,
        /// <summary>Multiplies turn duration by X while keeping magnitude / stacks fixed.</summary>
        Duration = 2,
        /// <summary>Multiplies both value/stacks and duration by X.</summary>
        Both = 3,
        /// <summary>Does not multiply values by X (still skipped when X &lt;= 0).</summary>
        None = 4
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "Effect")]
    [System.Serializable]
    public abstract class Effect
    {
        public abstract GameAction GetGameAction(List<CombatantView> targets, CombatantView caster);

        /// <summary>
        /// Produces the scaled GameAction for X-cost cards (xMultiplier = Mana spent).
        /// Returns null when xMultiplier &lt;= 0 so playing an X-cost card with 0 Mana deals no effect.
        /// Effects that scale numerically (Damage, Shield, Heal, Draw, Beam) override this to multiply values directly;
        /// all other effects cleanly repeat xMultiplier times in sequence.
        /// </summary>
        public virtual GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            if (xMultiplier == 1) return GetGameAction(targets, caster);

            List<GameAction> actions = new(xMultiplier);
            for (int i = 0; i < xMultiplier; i++)
            {
                GameAction action = GetGameAction(targets, caster);
                if (action != null) actions.Add(action);
            }
            return actions.Count > 0 ? new SequentialGameAction(actions) : null;
        }
    }
}