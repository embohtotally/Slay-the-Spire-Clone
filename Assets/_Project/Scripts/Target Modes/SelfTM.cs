using System.Collections.Generic;

public class SelfTM : TargetMode
{
    public override List<CombatantView> GetTargets(CombatantView caster = null)
    {
        if (caster == null) return new List<CombatantView>();
        return new List<CombatantView>() { caster };
    }
}
