using UnityEngine;

[System.Serializable]
public class DamageOverTimeData
{
    public CombatantView Target { get; private set; }
    public int DamagePerTurn { get; private set; }
    public int RemainingTurns { get; private set; }
    public CombatantView Caster { get; private set; }

    public DamageOverTimeData(CombatantView target, int damagePerTurn, int duration, CombatantView caster)
    {
        Target = target;
        DamagePerTurn = damagePerTurn;
        RemainingTurns = duration;
        Caster = caster;
    }

    public void DecrementDuration()
    {
        RemainingTurns--;
    }
}
