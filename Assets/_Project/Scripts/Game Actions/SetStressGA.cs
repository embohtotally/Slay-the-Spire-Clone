using System.Collections.Generic;

public class SetStressGA : GameAction, IHaveCaster
{
    public string SourceAllyName { get; private set; }
    public List<CombatantView> Targets { get; private set; }
    public CombatantView Caster { get; private set; }

    public SetStressGA(string sourceAllyName, List<CombatantView> targets, CombatantView caster)
    {
        SourceAllyName = sourceAllyName;
        Targets = targets;
        Caster = caster;
    }
}
