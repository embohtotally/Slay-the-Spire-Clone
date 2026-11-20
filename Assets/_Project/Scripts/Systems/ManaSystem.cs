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
        private int reservedMana;

        public int CurrentMana => currentMana;
        public int AvailableMana => Mathf.Max(0, currentMana - reservedMana);
        public int MaxMana => maxMana;
        public event Action<int, int> ManaChanged;

        protected override void Awake()
        {
            base.Awake();
            currentMana = maxMana;
            reservedMana = 0;
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
            reservedMana = 0;
            ActionSystem.DetachPerformer<SpendManaGA>();
            ActionSystem.DetachPerformer<RefillManaGA>();
            ActionSystem.DetachPerformer<ModifyManaGA>();
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(EnemyTurnPostReaction, ReactionTiming.POST);
        }

        public bool HasEnoughMana(int mana)
        {
            return AvailableMana >= mana;
        }

        /// <summary>
        /// Immediately reserves mana when a card play is queued so rapid-fire card plays
        /// update the Mana UI instantaneously and cannot overspend mana.
        /// </summary>
        public void ReserveMana(int amount)
        {
            if (amount <= 0) return;
            reservedMana += amount;
            UpdateManaDisplay();
        }

        public void ReleaseReservedMana(int amount)
        {
            if (amount <= 0) return;
            reservedMana = Mathf.Max(0, reservedMana - amount);
            UpdateManaDisplay();
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
            if (refillCurrent) reservedMana = 0;
            UpdateManaDisplay();
        }

        private void UpdateManaDisplay()
        {
            int displayMana = AvailableMana;
            if (manaUI != null) manaUI.UpdateManaText(displayMana);
            ManaChanged?.Invoke(displayMana, maxMana);
        }

        private IEnumerator SpendManaPerformer(SpendManaGA spendManaGA)
        {
            int amount = Mathf.Max(0, spendManaGA.Amount);
            currentMana = Mathf.Max(0, currentMana - amount);
            reservedMana = Mathf.Max(0, reservedMana - amount);
            UpdateManaDisplay();
            yield break;
        }

        private IEnumerator RefillManaPerformer(RefillManaGA refillManaGA)
        {
            currentMana = maxMana;
            reservedMana = 0;
            UpdateManaDisplay();
            yield break;
        }

        private IEnumerator ModifyManaPerformer(ModifyManaGA modifyManaGA)
        {
            currentMana = Mathf.Clamp(currentMana + modifyManaGA.Amount, 0, maxMana);
            UpdateManaDisplay();
            yield break;
        }

        private void EnemyTurnPostReaction(EnemyTurnGA enemyTurnGA)
        {
            if (HeroSystem.Instance.HeroView.IsStunned) return;

            RefillManaGA refillManaGA = new();
            ActionSystem.Instance.AddReaction(refillManaGA);
        }
    }
}
