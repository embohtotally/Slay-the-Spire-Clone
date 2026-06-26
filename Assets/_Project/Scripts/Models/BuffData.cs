[System.Serializable]
public class BuffData
{
    public CombatantView Target { get; private set; }
    public BuffType Type { get; private set; }
    public int Value { get; private set; }
    public int RemainingTurns { get; private set; }
    public CombatantView Caster { get; private set; }

    public BuffData(CombatantView target, BuffType type, int value, int remainingTurns, CombatantView caster)
    {
        Target = target;
        Type = type;
        Value = value;
        RemainingTurns = remainingTurns;
        Caster = caster;
    }

    public void DecrementDuration()
    {
        RemainingTurns--;
    }
}
