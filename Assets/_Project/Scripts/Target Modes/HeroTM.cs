using System.Collections.Generic;

public class HeroTM : TargetMode
{
    public override List<CombatantView> GetTargets(CombatantView caster = null)
    {
        return new List<CombatantView>() { HeroSystem.Instance.HeroView };
    }
}
