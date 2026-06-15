using System.Collections.Generic;

public class ApplyDoTGA : GameAction
{
    public List<CombatantView> Targets { get; private set; }
    public int DamagePerTurn { get; private set; }
    public int Duration { get; private set; }
    public CombatantView Caster { get; private set; }

    public ApplyDoTGA(List<CombatantView> targets, int damagePerTurn, int duration, CombatantView caster)
    {
        Targets = targets;
        DamagePerTurn = damagePerTurn;
        Duration = duration;
        Caster = caster;
    }
}
