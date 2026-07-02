using System;
using SerializeReferenceEditor;
using UnityEngine;

public enum RelicTriggerType
{
    CombatStarted,
    CardPlayed,
    CardsDrawn,
    ManaSpent,
    ManaRefilled,
    EnemyTurnStarted,
    PlayerTurnStarted,
    DamageDealt,
    DamageTaken,
    Healed,
    EnemyKilled,
    CombatWon
}

public enum RelicCombatantFilter
{
    Any,
    Hero,
    Enemy,
    None
}

[Serializable]
public class RelicReactionDefinition
{
    [SerializeField] private RelicTriggerType trigger = RelicTriggerType.CombatStarted;
    [SerializeField] private ReactionTiming timing = ReactionTiming.POST;
    [Tooltip("Optional filter for the action caster/source. Use Any for triggers that do not have a caster.")]
    [SerializeField] private RelicCombatantFilter casterFilter = RelicCombatantFilter.Any;
    [Tooltip("Optional filter for action targets. For DamageTaken, use Target Filter = Hero.")]
    [SerializeField] private RelicCombatantFilter targetFilter = RelicCombatantFilter.Any;
    [Tooltip("If true, this reaction can only trigger once until the next CombatStarted action.")]
    [SerializeField] private bool oncePerCombat;
    [SerializeReference, SR] private RelicReactionEffect effect;

    public RelicTriggerType Trigger => trigger;
    public ReactionTiming Timing => timing;
    public bool OncePerCombat => oncePerCombat;
    public RelicReactionEffect Effect => effect;

    public bool Matches(RelicTriggerContext context)
    {
        if (context == null || effect == null) return false;
        if (context.TriggerType != trigger || context.Timing != timing) return false;
        if (!MatchesFilter(context.Caster, casterFilter)) return false;
        if (!MatchesFilter(context.Targets, targetFilter)) return false;

        return true;
    }

    public GameAction CreateReaction(RelicTriggerContext context)
    {
        return effect != null ? effect.CreateGameAction(context) : null;
    }

    private static bool MatchesFilter(CombatantView combatant, RelicCombatantFilter filter)
    {
        return filter switch
        {
            RelicCombatantFilter.Any => true,
            RelicCombatantFilter.None => combatant == null,
            RelicCombatantFilter.Hero => combatant is HeroView,
            RelicCombatantFilter.Enemy => combatant is EnemyView,
            _ => true
        };
    }

    private static bool MatchesFilter(System.Collections.Generic.IReadOnlyList<CombatantView> combatants, RelicCombatantFilter filter)
    {
        if (filter == RelicCombatantFilter.Any) return true;

        bool hasCombatants = combatants != null && combatants.Count > 0;
        if (filter == RelicCombatantFilter.None) return !hasCombatants;
        if (!hasCombatants) return false;

        foreach (CombatantView combatant in combatants)
        {
            if (MatchesFilter(combatant, filter)) return true;
        }

        return false;
    }
}
