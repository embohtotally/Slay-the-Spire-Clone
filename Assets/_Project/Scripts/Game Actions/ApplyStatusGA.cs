using System.Collections.Generic;

public class ApplyStatusGA : GameAction, IHaveCaster
{
    public List<CombatantView> Targets { get; private set; }
    public StatusType Status { get; private set; }
    public int Stacks { get; private set; }
    public int Duration { get; private set; }
    public CombatantView Caster { get; private set; }

    public ApplyStatusGA(List<CombatantView> targets, StatusType status, int stacks, int duration, CombatantView caster)
    {
        Targets = targets;
        Status = status;
        Stacks = stacks;
        Duration = duration;
        Caster = caster;
    }
}
