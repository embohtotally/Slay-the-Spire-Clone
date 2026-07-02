using NaughtyAttributes;
using UnityEngine;

[DisallowMultipleComponent]
public class GameActionAudioDirector : MonoBehaviour
{
    [Header("Combat Flow")]
    [SerializeField] private TuneSfxCue combatStartedSfx;
    [SerializeField] private TuneSfxCue enemyTurnSfx;
    [SerializeField] private TuneSfxCue combatWonSfx;

    [Header("Cards")]
    [SerializeField] private TuneSfxCue playCardSfx;
    [SerializeField] private TuneSfxCue drawCardsBatchSfx;
    [SerializeField] private TuneSfxCue discardHandSfx;

    [Header("Resource Changes")]
    [SerializeField] private TuneSfxCue spendManaSfx;
    [SerializeField] private TuneSfxCue refillManaSfx;

    [Header("Hit / Recovery")]
    [SerializeField] private TuneSfxCue dealDamageSfx;
    [SerializeField] private TuneSfxCue healSfx;
    [SerializeField] private TuneSfxCue gainShieldSfx;

    [Header("Status / Stress")]
    [SerializeField] private TuneSfxCue applyBuffSfx;
    [SerializeField] private TuneSfxCue applyStatusSfx;
    [SerializeField] private TuneSfxCue removeDebuffSfx;
    [SerializeField] private TuneSfxCue stressGainSfx;
    [SerializeField] private TuneSfxCue stressReduceSfx;

    [Header("Enemies")]
    [SerializeField] private TuneSfxCue enemyKilledSfx;
    [SerializeField] private TuneSfxCue summonEnemySfx;

    private void OnEnable()
    {
        ActionSystem.SubscribeReaction<CombatStartedGA>(OnCombatStarted, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurn, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<CombatWonGA>(OnCombatWon, ReactionTiming.POST);

        ActionSystem.SubscribeReaction<PlayCardGA>(OnPlayCard, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<DrawCardsGA>(OnDrawCards, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<DiscardAllCardsGA>(OnDiscardAllCards, ReactionTiming.PRE);

        ActionSystem.SubscribeReaction<SpendManaGA>(OnSpendMana, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<RefillManaGA>(OnRefillMana, ReactionTiming.POST);

        ActionSystem.SubscribeReaction<DealDamageGA>(OnDealDamage, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<HealGA>(OnHeal, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<GainShieldGA>(OnGainShield, ReactionTiming.POST);

        ActionSystem.SubscribeReaction<ApplyBuffGA>(OnApplyBuff, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<ApplyStatusGA>(OnApplyStatus, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<RemoveDebuffGA>(OnRemoveDebuff, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<AddStressGA>(OnAddStress, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<ModifyStressGA>(OnModifyStress, ReactionTiming.POST);

        ActionSystem.SubscribeReaction<KillEnemyGA>(OnKillEnemy, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<SummonEnemyGA>(OnSummonEnemy, ReactionTiming.POST);
    }

    private void OnDisable()
    {
        ActionSystem.UnsubscribeReaction<CombatStartedGA>(OnCombatStarted, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurn, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<CombatWonGA>(OnCombatWon, ReactionTiming.POST);

        ActionSystem.UnsubscribeReaction<PlayCardGA>(OnPlayCard, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<DrawCardsGA>(OnDrawCards, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<DiscardAllCardsGA>(OnDiscardAllCards, ReactionTiming.PRE);

        ActionSystem.UnsubscribeReaction<SpendManaGA>(OnSpendMana, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<RefillManaGA>(OnRefillMana, ReactionTiming.POST);

        ActionSystem.UnsubscribeReaction<DealDamageGA>(OnDealDamage, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<HealGA>(OnHeal, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<GainShieldGA>(OnGainShield, ReactionTiming.POST);

        ActionSystem.UnsubscribeReaction<ApplyBuffGA>(OnApplyBuff, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<ApplyStatusGA>(OnApplyStatus, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<RemoveDebuffGA>(OnRemoveDebuff, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<AddStressGA>(OnAddStress, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<ModifyStressGA>(OnModifyStress, ReactionTiming.POST);

        ActionSystem.UnsubscribeReaction<KillEnemyGA>(OnKillEnemy, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<SummonEnemyGA>(OnSummonEnemy, ReactionTiming.POST);
    }

    [Button("Play Combat Won SFX", EButtonEnableMode.Playmode)]
    private void PreviewCombatWonSfx()
    {
        combatWonSfx?.Play(this, transform);
    }

    private void OnCombatStarted(CombatStartedGA action) => combatStartedSfx?.Play(this, transform);
    private void OnEnemyTurn(EnemyTurnGA action) => enemyTurnSfx?.Play(this, transform);
    private void OnCombatWon(CombatWonGA action) => combatWonSfx?.Play(this, transform);

    private void OnPlayCard(PlayCardGA action) => playCardSfx?.Play(this, transform);
    private void OnDrawCards(DrawCardsGA action) => drawCardsBatchSfx?.Play(this, transform);
    private void OnDiscardAllCards(DiscardAllCardsGA action) => discardHandSfx?.Play(this, transform);

    private void OnSpendMana(SpendManaGA action) => spendManaSfx?.Play(this, transform);
    private void OnRefillMana(RefillManaGA action) => refillManaSfx?.Play(this, transform);

    private void OnDealDamage(DealDamageGA action) => PlayForFirstTarget(dealDamageSfx, action.Targets);
    private void OnHeal(HealGA action) => PlayForFirstTarget(healSfx, action.Targets);
    private void OnGainShield(GainShieldGA action) => PlayForFirstTarget(gainShieldSfx, action.Targets);

    private void OnApplyBuff(ApplyBuffGA action) => PlayForFirstTarget(applyBuffSfx, action.Targets);
    private void OnApplyStatus(ApplyStatusGA action) => PlayForFirstTarget(applyStatusSfx, action.Targets);
    private void OnRemoveDebuff(RemoveDebuffGA action) => PlayForFirstTarget(removeDebuffSfx, action.Targets);
    private void OnAddStress(AddStressGA action) => PlayForFirstTarget(stressGainSfx, action.Targets);

    private void OnModifyStress(ModifyStressGA action)
    {
        TuneSfxCue cue = action.Amount >= 0 ? stressGainSfx : stressReduceSfx;
        PlayForFirstTarget(cue, action.Targets);
    }

    private void OnKillEnemy(KillEnemyGA action) => enemyKilledSfx?.Play(this, action.EnemyView != null ? action.EnemyView.transform : transform);
    private void OnSummonEnemy(SummonEnemyGA action) => summonEnemySfx?.Play(this, transform);

    private void PlayForFirstTarget(TuneSfxCue cue, System.Collections.Generic.IReadOnlyList<CombatantView> targets)
    {
        if (cue == null) return;
        Transform target = targets != null && targets.Count > 0 && targets[0] != null ? targets[0].transform : transform;
        cue.Play(this, target);
    }
}
