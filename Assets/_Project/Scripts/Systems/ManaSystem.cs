using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ManaSystem : Singleton<ManaSystem>
{
    [SerializeField] private ManaUI manaUI;

    [SerializeField] int maxMana = 3;

    private int currentMana;

    protected override void Awake()
    {
        base.Awake();
        currentMana = maxMana;
        manaUI.UpdateManaText(currentMana);
    }

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<SpendManaGA>(SpendManaPerformer);
        ActionSystem.AttachPerformer<RefillManaGA>(RefillManaPerformer);
        ActionSystem.AttachPerformer<ModifyManaGA>(ModifyManaPerformer);

        ActionSystem.SubscribeReaction<EnemyTurnGA>(EnemyTurnPostReaction, ReactionTiming.POST);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<SpendManaGA>();
        ActionSystem.DetachPerformer<RefillManaGA>();
        ActionSystem.DetachPerformer<ModifyManaGA>();
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(EnemyTurnPostReaction, ReactionTiming.POST);
    }

    public bool HasEnoughMana(int mana)
    {
        return currentMana >= mana;
    }

    private IEnumerator SpendManaPerformer(SpendManaGA spendManaGA)
    {
        currentMana -= spendManaGA.Amount;
        manaUI.UpdateManaText(currentMana);
        yield return null;
    }

    private IEnumerator RefillManaPerformer(RefillManaGA refillManaGA)
    {
        currentMana = maxMana;
        manaUI.UpdateManaText(currentMana);
        yield return null;
    }

    private IEnumerator ModifyManaPerformer(ModifyManaGA modifyManaGA)
    {
        currentMana = Mathf.Clamp(currentMana + modifyManaGA.Amount, 0, maxMana);
        manaUI.UpdateManaText(currentMana);
        yield return null;
    }

    private void EnemyTurnPostReaction(EnemyTurnGA enemyTurnGA)
    {
        if (HeroSystem.Instance.HeroView.IsStunned) return;

        RefillManaGA refillManaGA = new();
        ActionSystem.Instance.AddReaction(refillManaGA);
    }
}