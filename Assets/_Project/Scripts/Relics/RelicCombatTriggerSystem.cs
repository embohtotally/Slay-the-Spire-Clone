using System.Collections.Generic;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;

[DisallowMultipleComponent]
public class RelicCombatTriggerSystem : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool logTriggeredRelics;
    [SerializeField] private bool logMissingRelicManager;

    private readonly HashSet<string> oncePerCombatKeys = new();

    private void OnEnable()
    {
        ActionSystem.SubscribeReaction<CombatStartedGA>(OnCombatStartedPost, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<PlayCardGA>(OnCardPlayedPost, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<DrawCardsGA>(OnCardsDrawnPost, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<SpendManaGA>(OnManaSpentPost, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<RefillManaGA>(OnManaRefilledPost, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnStartedPre, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(OnPlayerTurnStartedPost, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<DealDamageGA>(OnDamageDealtPost, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<DealDamageGA>(OnDamageTakenPost, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<HealGA>(OnHealedPost, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<KillEnemyGA>(OnEnemyKilledPost, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<CombatWonGA>(OnCombatWonPost, ReactionTiming.POST);
    }

    private void OnDisable()
    {
        ActionSystem.UnsubscribeReaction<CombatStartedGA>(OnCombatStartedPost, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<PlayCardGA>(OnCardPlayedPost, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<DrawCardsGA>(OnCardsDrawnPost, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<SpendManaGA>(OnManaSpentPost, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<RefillManaGA>(OnManaRefilledPost, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnStartedPre, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnPlayerTurnStartedPost, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<DealDamageGA>(OnDamageDealtPost, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<DealDamageGA>(OnDamageTakenPost, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<HealGA>(OnHealedPost, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<KillEnemyGA>(OnEnemyKilledPost, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<CombatWonGA>(OnCombatWonPost, ReactionTiming.POST);
    }

    [Button("Test Combat Started Trigger", EButtonEnableMode.Playmode)]
    public void TestCombatStartedTrigger()
    {
        if (ActionSystem.Instance == null)
        {
            Gameseed26.Logger.LogWarning(this, "Cannot test relic trigger because ActionSystem is missing.");
            return;
        }

        ActionSystem.Instance.Perform(new CombatStartedGA());
    }

    private void OnCombatStartedPost(CombatStartedGA action)
    {
        oncePerCombatKeys.Clear();
        TriggerRelics(RelicTriggerType.CombatStarted, ReactionTiming.POST, action);
    }

    private void OnCardPlayedPost(PlayCardGA action)
    {
        TriggerRelics(RelicTriggerType.CardPlayed, ReactionTiming.POST, action);
    }

    private void OnCardsDrawnPost(DrawCardsGA action)
    {
        TriggerRelics(RelicTriggerType.CardsDrawn, ReactionTiming.POST, action);
    }

    private void OnManaSpentPost(SpendManaGA action)
    {
        TriggerRelics(RelicTriggerType.ManaSpent, ReactionTiming.POST, action);
    }

    private void OnManaRefilledPost(RefillManaGA action)
    {
        TriggerRelics(RelicTriggerType.ManaRefilled, ReactionTiming.POST, action);
    }

    private void OnEnemyTurnStartedPre(EnemyTurnGA action)
    {
        TriggerRelics(RelicTriggerType.EnemyTurnStarted, ReactionTiming.PRE, action);
    }

    private void OnPlayerTurnStartedPost(EnemyTurnGA action)
    {
        TriggerRelics(RelicTriggerType.PlayerTurnStarted, ReactionTiming.POST, action);
    }

    private void OnDamageDealtPost(DealDamageGA action)
    {
        TriggerRelics(RelicTriggerType.DamageDealt, ReactionTiming.POST, action);
    }

    private void OnDamageTakenPost(DealDamageGA action)
    {
        TriggerRelics(RelicTriggerType.DamageTaken, ReactionTiming.POST, action);
    }

    private void OnHealedPost(HealGA action)
    {
        TriggerRelics(RelicTriggerType.Healed, ReactionTiming.POST, action);
    }

    private void OnEnemyKilledPost(KillEnemyGA action)
    {
        TriggerRelics(RelicTriggerType.EnemyKilled, ReactionTiming.POST, action);
    }

    private void OnCombatWonPost(CombatWonGA action)
    {
        TriggerRelics(RelicTriggerType.CombatWon, ReactionTiming.POST, action);
    }

    private void TriggerRelics(RelicTriggerType triggerType, ReactionTiming timing, GameAction sourceAction)
    {
        if (ActionSystem.Instance == null) return;

        RunRelicManager relicManager = RunRelicManager.Instance;
        if (relicManager == null || !relicManager.HasRelics)
        {
            if (logMissingRelicManager)
            {
                Gameseed26.Logger.Log(this, "No RunRelicManager/relics found for relic trigger check.");
            }
            return;
        }

        List<RelicData> relics = relicManager.GetRelicsCopy();
        for (int relicIndex = 0; relicIndex < relics.Count; relicIndex++)
        {
            RelicData relic = relics[relicIndex];
            if (relic == null || relic.Reactions == null) continue;

            for (int reactionIndex = 0; reactionIndex < relic.Reactions.Count; reactionIndex++)
            {
                RelicReactionDefinition reaction = relic.Reactions[reactionIndex];
                if (reaction == null) continue;

                string onceKey = BuildOnceKey(relic, relicIndex, reactionIndex);
                if (reaction.OncePerCombat && oncePerCombatKeys.Contains(onceKey)) continue;

                RelicTriggerContext context = BuildContext(relic, triggerType, timing, sourceAction);
                if (!reaction.Matches(context)) continue;

                GameAction reactionAction = reaction.CreateReaction(context);
                if (reactionAction == null) continue;

                ActionSystem.Instance.AddReaction(reactionAction);
                if (reaction.OncePerCombat) oncePerCombatKeys.Add(onceKey);

                if (logTriggeredRelics)
                {
                    Gameseed26.Logger.Log(this, $"Relic '{relic.Title}' triggered on {triggerType} and queued {reactionAction.GetType().Name}.");
                }
            }
        }
    }

    private static RelicTriggerContext BuildContext(RelicData relic, RelicTriggerType triggerType, ReactionTiming timing, GameAction sourceAction)
    {
        CombatantView caster = null;
        List<CombatantView> targets = null;
        Card playedCard = null;

        if (sourceAction is IHaveCaster casterAction)
        {
            caster = casterAction.Caster;
        }

        switch (sourceAction)
        {
            case PlayCardGA playCardGA:
                caster = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : caster;
                playedCard = playCardGA.Card;
                if (playCardGA.ManualTarget != null) targets = new List<CombatantView> { playCardGA.ManualTarget };
                break;
            case DealDamageGA dealDamageGA:
                targets = CopyTargets(dealDamageGA.Targets);
                break;
            case HealGA healGA:
                targets = CopyTargets(healGA.Targets);
                break;
            case GainShieldGA gainShieldGA:
                targets = CopyTargets(gainShieldGA.Targets);
                break;
            case ApplyBuffGA applyBuffGA:
                targets = CopyTargets(applyBuffGA.Targets);
                break;
            case ApplyStatusGA applyStatusGA:
                targets = CopyTargets(applyStatusGA.Targets);
                break;
            case KillEnemyGA killEnemyGA:
                if (killEnemyGA.EnemyView != null) targets = new List<CombatantView> { killEnemyGA.EnemyView };
                break;
        }

        return new RelicTriggerContext(relic, triggerType, timing, sourceAction, caster, targets, playedCard);
    }

    private static List<CombatantView> CopyTargets(IReadOnlyList<CombatantView> sourceTargets)
    {
        if (sourceTargets == null) return null;

        List<CombatantView> targets = new();
        foreach (CombatantView target in sourceTargets)
        {
            if (target != null) targets.Add(target);
        }

        return targets;
    }

    private static string BuildOnceKey(RelicData relic, int relicIndex, int reactionIndex)
    {
        int relicId = relic != null ? relic.GetInstanceID() : 0;
        return $"{relicId}:{relicIndex}:{reactionIndex}";
    }
}
