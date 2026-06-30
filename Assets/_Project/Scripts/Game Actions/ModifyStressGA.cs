using System.Collections.Generic;

public class ModifyStressGA : GameAction, IHaveCaster
{
    public List<CombatantView> Targets { get; private set; }
    public int Amount { get; private set; }
    public CombatantView Caster { get; private set; }

    public ModifyStressGA(int amount, List<CombatantView> targets, CombatantView caster)
    {
        Amount = amount;
        Targets = targets;
        Caster = caster;
    }
}
