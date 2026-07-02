using UnityEngine;

[DisallowMultipleComponent]
public class GameActionVfxDirector : MonoBehaviour
{
    [Header("Combat Flow")]
    [SerializeField] private VfxCue combatStartedVfx = new();
    [SerializeField] private VfxCue enemyTurnVfx = new();
    [SerializeField] private VfxCue playerTurnVfx = new();
    [SerializeField] private VfxCue combatWonVfx = new();

    [Header("Card Flow")]
    [SerializeField] private VfxCue playCardVfx = new();
    [SerializeField] private VfxCue drawCardsVfx = new();
    [SerializeField] private VfxCue discardHandVfx = new();

    [Header("Mana")]
    [SerializeField] private VfxCue spendManaVfx = new();
    [SerializeField] private VfxCue refillManaVfx = new();
    [SerializeField] private VfxCue gainManaVfx = new();
    [SerializeField] private VfxCue loseManaVfx = new();

    [Header("Combatant Effects")]
    [SerializeField] private VfxCue dealDamageVfx = new();
    [SerializeField] private VfxCue healVfx = new();
    [SerializeField] private VfxCue gainShieldVfx = new();
    [SerializeField] private VfxCue applyBuffVfx = new();
    [SerializeField] private VfxCue applyStatusVfx = new();
    [SerializeField] private VfxCue applyDotVfx = new();
    [SerializeField] private VfxCue removeDebuffVfx = new();
    [SerializeField] private VfxCue stunVfx = new();
    [SerializeField] private VfxCue tauntVfx = new();

    [Header("Stress")]
    [SerializeField] private VfxCue stressGainVfx = new();
    [SerializeField] private VfxCue stressReduceVfx = new();
    [SerializeField] private VfxCue stressSetVfx = new();

    [Header("Enemy")]
    [SerializeField] private VfxCue enemyKilledVfx = new();
    [SerializeField] private VfxCue summonEnemyVfx = new();
    [SerializeField] private VfxCue enemyIntentVfx = new();

    [Header("Items / Rewards")]
    [SerializeField] private VfxCue usePotionVfx = new();
    [SerializeField] private VfxCue openCardRewardVfx = new();

    private void OnEnable()
    {
        ActionSystem.SubscribeReaction<CombatStartedGA>(OnCombatStarted, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurn, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(OnPlayerTurn, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<CombatWonGA>(OnCombatWon, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<PlayCardGA>(OnPlayCard, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<DrawCardsGA>(OnDrawCards, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<DiscardAllCardsGA>(OnDiscardHand, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<SpendManaGA>(OnSpendMana, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<RefillManaGA>(OnRefillMana, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<ModifyManaGA>(OnModifyMana, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<DealDamageGA>(OnDealDamage, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<HealGA>(OnHeal, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<GainShieldGA>(OnGainShield, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<ApplyBuffGA>(OnApplyBuff, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<ApplyStatusGA>(OnApplyStatus, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<ApplyDoTGA>(OnApplyDot, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<RemoveDebuffGA>(OnRemoveDebuff, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<ApplyStunGA>(OnApplyStun, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<ApplyTauntGA>(OnApplyTaunt, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<AddStressGA>(OnAddStress, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<ModifyStressGA>(OnModifyStress, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<SetStressGA>(OnSetStress, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<KillEnemyGA>(OnKillEnemy, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<SummonEnemyGA>(OnSummonEnemy, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<ExecuteEnemyIntentGA>(OnEnemyIntent, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<UsePotionGA>(OnUsePotion, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<OpenCardRewardGA>(OnOpenCardReward, ReactionTiming.PRE);
    }

    private void OnDisable()
    {
        ActionSystem.UnsubscribeReaction<CombatStartedGA>(OnCombatStarted, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurn, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnPlayerTurn, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<CombatWonGA>(OnCombatWon, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<PlayCardGA>(OnPlayCard, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<DrawCardsGA>(OnDrawCards, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<DiscardAllCardsGA>(OnDiscardHand, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<SpendManaGA>(OnSpendMana, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<RefillManaGA>(OnRefillMana, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<ModifyManaGA>(OnModifyMana, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<DealDamageGA>(OnDealDamage, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<HealGA>(OnHeal, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<GainShieldGA>(OnGainShield, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<ApplyBuffGA>(OnApplyBuff, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<ApplyStatusGA>(OnApplyStatus, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<ApplyDoTGA>(OnApplyDot, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<RemoveDebuffGA>(OnRemoveDebuff, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<ApplyStunGA>(OnApplyStun, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<ApplyTauntGA>(OnApplyTaunt, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<AddStressGA>(OnAddStress, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<ModifyStressGA>(OnModifyStress, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<SetStressGA>(OnSetStress, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<KillEnemyGA>(OnKillEnemy, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<SummonEnemyGA>(OnSummonEnemy, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<ExecuteEnemyIntentGA>(OnEnemyIntent, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<UsePotionGA>(OnUsePotion, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<OpenCardRewardGA>(OnOpenCardReward, ReactionTiming.PRE);
    }

    private void OnCombatStarted(CombatStartedGA action) => combatStartedVfx.Play(HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
    private void OnEnemyTurn(EnemyTurnGA action) => enemyTurnVfx.Play(HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
    private void OnPlayerTurn(EnemyTurnGA action) => playerTurnVfx.Play(HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
    private void OnCombatWon(CombatWonGA action) => combatWonVfx.Play(HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
    private void OnPlayCard(PlayCardGA action) => playCardVfx.Play(action.ManualTarget != null ? action.ManualTarget : (HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null));
    private void OnDrawCards(DrawCardsGA action) => drawCardsVfx.Play(HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
    private void OnDiscardHand(DiscardAllCardsGA action) => discardHandVfx.Play(HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
    private void OnSpendMana(SpendManaGA action) => spendManaVfx.Play(HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
    private void OnRefillMana(RefillManaGA action) => refillManaVfx.Play(HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
    private void OnModifyMana(ModifyManaGA action) => (action.Amount >= 0 ? gainManaVfx : loseManaVfx).Play(HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
    private void OnDealDamage(DealDamageGA action) => dealDamageVfx.Play(action.Targets);
    private void OnHeal(HealGA action) => healVfx.Play(action.Targets);
    private void OnGainShield(GainShieldGA action) => gainShieldVfx.Play(action.Targets);
    private void OnApplyBuff(ApplyBuffGA action) => applyBuffVfx.Play(action.Targets);
    private void OnApplyStatus(ApplyStatusGA action) => applyStatusVfx.Play(action.Targets);
    private void OnApplyDot(ApplyDoTGA action) => applyDotVfx.Play(action.Targets);
    private void OnRemoveDebuff(RemoveDebuffGA action) => removeDebuffVfx.Play(action.Targets);
    private void OnApplyStun(ApplyStunGA action) => stunVfx.Play(action.Targets);
    private void OnApplyTaunt(ApplyTauntGA action) => tauntVfx.Play(action.Targets);
    private void OnAddStress(AddStressGA action) => stressGainVfx.Play(action.Targets);
    private void OnModifyStress(ModifyStressGA action) => (action.Amount >= 0 ? stressGainVfx : stressReduceVfx).Play(action.Targets);
    private void OnSetStress(SetStressGA action) => stressSetVfx.Play(action.Targets);
    private void OnKillEnemy(KillEnemyGA action) => enemyKilledVfx.Play(action.EnemyView);
    private void OnSummonEnemy(SummonEnemyGA action) => summonEnemyVfx.Play(action.Caster);
    private void OnEnemyIntent(ExecuteEnemyIntentGA action) => enemyIntentVfx.Play(action.Attacker);
    private void OnUsePotion(UsePotionGA action) => usePotionVfx.Play(HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
    private void OnOpenCardReward(OpenCardRewardGA action) => openCardRewardVfx.Play(HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
}
