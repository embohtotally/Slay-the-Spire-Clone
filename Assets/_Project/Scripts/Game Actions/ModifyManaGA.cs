public class ModifyManaGA : GameAction, IHaveCaster
{
    public int Amount { get; private set; }
    public CombatantView Caster { get; private set; }

    public ModifyManaGA(int amount, CombatantView caster)
    {
        Amount = amount;
        Caster = caster;
    }
}
