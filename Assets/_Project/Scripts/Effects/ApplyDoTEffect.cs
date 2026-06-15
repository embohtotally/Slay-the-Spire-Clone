using System.Collections.Generic;
using UnityEngine;

public class ApplyDoTEffect : Effect
{
    [SerializeField] private int damagePerTurn;
    [SerializeField] private int duration;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        // For multiple targets, typically Slay the Spire applies the same effect to each.
        // We'll return an ApplyDoTGA for the first target in this simple implementation,
        // or we could loop through and add multiple reactions.
        // To handle multiple targets, the best approach is to let the GameAction handle multiple targets,
        // or just add a loop. Let's make ApplyDoTGA take a List<CombatantView> Targets instead?
        // Wait, the GA pattern usually supports multiple targets. Let's update ApplyDoTGA to take List<CombatantView>.
        
        ApplyDoTGA applyDoTGA = new(targets, damagePerTurn, duration, caster);
        return applyDoTGA;
    }
}
