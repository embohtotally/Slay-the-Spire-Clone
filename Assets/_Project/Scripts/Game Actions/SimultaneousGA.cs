using System.Collections.Generic;

public class SimultaneousGA : GameAction, IHaveCaster
{
    public List<GameAction> Actions { get; private set; }
    public CombatantView Caster { get; private set; }

    public SimultaneousGA(List<GameAction> actions, CombatantView caster)
    {
        Actions = actions;
        Caster = caster;
    }
}
