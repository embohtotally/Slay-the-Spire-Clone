using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [System.Serializable]
    public class ManaSystem : Singleton<ManaSystem>
    {
        [SerializeField] private ManaUI manaUI;

        [SerializeField] private int maxMana = 3;

        private int currentMana;

        public int CurrentMana => currentMana;
        public int MaxMana => maxMana;
        public event Action<int, int> ManaChanged;

        protected override void Awake()
        {
            base.Awake();
            currentMana = maxMana;
            UpdateManaDisplay();
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

        public void ModifyMaxMana(int delta, bool alsoModifyCurrent = false)
        {
            maxMana = Mathf.Max(0, maxMana + delta);
            if (alsoModifyCurrent)
            {
                currentMana = Mathf.Clamp(currentMana + delta, 0, maxMana);
            }
            else
            {
                currentMana = Mathf.Clamp(currentMana, 0, maxMana);
            }
            UpdateManaDisplay();
        }

        public void SetMaxMana(int newMaxMana, bool refillCurrent = false)
        {
            maxMana = Mathf.Max(0, newMaxMana);
            currentMana = refillCurrent ? maxMana : Mathf.Clamp(currentMana, 0, maxMana);
            UpdateManaDisplay();
        }

        private void UpdateManaDisplay()
        {
            if (manaUI != null) manaUI.UpdateManaText(currentMana);
            ManaChanged?.Invoke(currentMana, maxMana);
        }

        private IEnumerator SpendManaPerformer(SpendManaGA spendManaGA)
        {
            currentMana = Mathf.Max(0, currentMana - spendManaGA.Amount);
            UpdateManaDisplay();
            yield return null;
        }

        private IEnumerator RefillManaPerformer(RefillManaGA refillManaGA)
        {
            currentMana = maxMana;
            UpdateManaDisplay();
            yield return null;
        }

        private IEnumerator ModifyManaPerformer(ModifyManaGA modifyManaGA)
        {
            currentMana = Mathf.Clamp(currentMana + modifyManaGA.Amount, 0, maxMana);
            UpdateManaDisplay();
            yield return null;
        }

        private void EnemyTurnPostReaction(EnemyTurnGA enemyTurnGA)
        {
            if (HeroSystem.Instance.HeroView.IsStunned) return;

            RefillManaGA refillManaGA = new();
            ActionSystem.Instance.AddReaction(refillManaGA);
        }
    }
}
