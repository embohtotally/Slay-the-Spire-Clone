using System.Collections.Generic;

public class RemoveDebuffGA : GameAction, IHaveCaster
{
    public List<CombatantView> Targets { get; private set; }
    public int Count { get; private set; }
    public CombatantView Caster { get; private set; }

    public RemoveDebuffGA(int count, List<CombatantView> targets, CombatantView caster)
    {
        Count = count;
        Targets = targets;
        Caster = caster;
    }
}
