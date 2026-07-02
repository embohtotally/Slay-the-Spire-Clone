using System;
using System.Collections.Generic;
using UnityEngine;

public enum RelicReactionTargetSelection
{
    Hero,
    OriginalCaster,
    OriginalTargets,
    AllEnemies
}

[Serializable]
public abstract class RelicReactionEffect
{
    public abstract GameAction CreateGameAction(RelicTriggerContext context);

    protected static CombatantView GetHero()
    {
        return HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
    }

    protected static List<CombatantView> ResolveTargets(RelicTriggerContext context, RelicReactionTargetSelection targetSelection)
    {
        List<CombatantView> targets = new();

        switch (targetSelection)
        {
            case RelicReactionTargetSelection.Hero:
                AddIfNotNull(targets, GetHero());
                break;
            case RelicReactionTargetSelection.OriginalCaster:
                AddIfNotNull(targets, context?.Caster);
                break;
            case RelicReactionTargetSelection.OriginalTargets:
                if (context?.Targets != null)
                {
                    foreach (CombatantView target in context.Targets)
                    {
                        AddIfNotNull(targets, target);
                    }
                }
                break;
            case RelicReactionTargetSelection.AllEnemies:
                if (EnemySystem.Instance != null && EnemySystem.Instance.Enemies != null)
                {
                    foreach (EnemyView enemy in EnemySystem.Instance.Enemies)
                    {
                        AddIfNotNull(targets, enemy);
                    }
                }
                break;
        }

        return targets;
    }

    private static void AddIfNotNull(List<CombatantView> targets, CombatantView target)
    {
        if (target != null) targets.Add(target);
    }
}

[Serializable]
public class RelicDrawCardsReactionEffect : RelicReactionEffect
{
    [SerializeField, Min(1)] private int amount = 1;

    public override GameAction CreateGameAction(RelicTriggerContext context)
    {
        return new DrawCardsGA(amount);
    }
}

[Serializable]
public class RelicModifyManaReactionEffect : RelicReactionEffect
{
    [SerializeField] private int amount = 1;

    public override GameAction CreateGameAction(RelicTriggerContext context)
    {
        return new ModifyManaGA(amount, GetHero());
    }
}

[Serializable]
public class RelicHealReactionEffect : RelicReactionEffect
{
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField] private RelicReactionTargetSelection targetSelection = RelicReactionTargetSelection.Hero;

    public override GameAction CreateGameAction(RelicTriggerContext context)
    {
        List<CombatantView> targets = ResolveTargets(context, targetSelection);
        return targets.Count > 0 ? new HealGA(amount, targets, GetHero()) : null;
    }
}

[Serializable]
public class RelicGainShieldReactionEffect : RelicReactionEffect
{
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField] private RelicReactionTargetSelection targetSelection = RelicReactionTargetSelection.Hero;

    public override GameAction CreateGameAction(RelicTriggerContext context)
    {
        List<CombatantView> targets = ResolveTargets(context, targetSelection);
        return targets.Count > 0 ? new GainShieldGA(amount, targets, GetHero()) : null;
    }
}

[Serializable]
public class RelicApplyBuffReactionEffect : RelicReactionEffect
{
    [SerializeField] private BuffType buffType = BuffType.Strength;
    [SerializeField] private int value = 1;
    [SerializeField, Min(1)] private int duration = 1;
    [SerializeField] private RelicReactionTargetSelection targetSelection = RelicReactionTargetSelection.Hero;

    public override GameAction CreateGameAction(RelicTriggerContext context)
    {
        List<CombatantView> targets = ResolveTargets(context, targetSelection);
        return targets.Count > 0 ? new ApplyBuffGA(targets, buffType, value, duration, GetHero()) : null;
    }
}
