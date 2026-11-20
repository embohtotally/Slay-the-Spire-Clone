using System;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Tutorials;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    public enum CardPlayFailure
    {
        None,
        MissingBackend,
        NotInHand,
        Busy,
        Blocked,
        NotEnoughMana,
        InvalidTarget,
        TutorialBlocked,
        InvalidDrop
    }

    [DisallowMultipleComponent]
    public class CardPlayService : MonoBehaviour
    {
        [SerializeField] private CardSystem _cardSystem;
        [SerializeField] private ActionSystem _actionSystem;
        [SerializeField] private ManaSystem _manaSystem;
        [SerializeField] private HeroView _heroView;
        [SerializeField] private CardTargetResolver _targetResolver;
        [SerializeField] private TutorialManager _tutorial;

        /// <summary>
        /// True while ActionSystem is executing any action. No longer blocks hovering or FIFO card queueing.
        /// </summary>
        public bool IsBusy => _actionSystem != null && _actionSystem.IsPerforming;

        /// <summary>
        /// True only when Enemy Turn is active or already queued — blocks card plays while letting
        /// the player rapidly chain cards and End Turn during their own turn.
        /// </summary>
        public bool IsTurnLocked =>
            (_actionSystem != null && _actionSystem.IsEnemyTurnQueuedOrActive) ||
            (EnemySystem.Instance != null && EnemySystem.Instance.IsEnemyTurnActive);

        public event Action StateChanged;

        private void OnEnable()
        {
            if (_actionSystem != null)
            {
                _actionSystem.BusyChanged += OnBusyChanged;
                _actionSystem.TurnStateChanged += RefreshState;
            }
            if (_manaSystem != null) _manaSystem.ManaChanged += OnManaChanged;
            if (_heroView != null) _heroView.StatusEffectsChanged += RefreshState;
        }

        private void OnDisable()
        {
            if (_actionSystem != null)
            {
                _actionSystem.BusyChanged -= OnBusyChanged;
                _actionSystem.TurnStateChanged -= RefreshState;
            }
            if (_manaSystem != null) _manaSystem.ManaChanged -= OnManaChanged;
            if (_heroView != null) _heroView.StatusEffectsChanged -= RefreshState;
            RefreshState();
        }

        public int GetEffectiveManaCost(Card card) =>
            _cardSystem != null ? _cardSystem.GetEffectiveManaCost(card) : card?.Mana ?? 0;

        public CardPlayFailure GetFailure(Card card, CombatantView target = null, bool requireTarget = true)
        {
            if (!isActiveAndEnabled || _cardSystem == null || !_cardSystem.isActiveAndEnabled ||
                _actionSystem == null || !_actionSystem.isActiveAndEnabled ||
                _manaSystem == null || !_manaSystem.isActiveAndEnabled ||
                _heroView == null || !_heroView.isActiveAndEnabled)
                return CardPlayFailure.MissingBackend;
            if (!_cardSystem.ContainsCard(card)) return CardPlayFailure.NotInHand;
            if (IsTurnLocked) return CardPlayFailure.Busy;
            if (_heroView.CurrentHealth <= 0 ||
                card.HasKeyword(PlushieOdyssey.Data.CardKeywords.Unplayable) ||
                (card.Type == PlushieOdyssey.Data.CardType.Attack && _heroView.HealthPenalty > 0))
                return CardPlayFailure.Blocked;
            if (_tutorial != null && _tutorial.isActiveAndEnabled && !_tutorial.CanInteractWithCard(card))
                return CardPlayFailure.TutorialBlocked;
            if (!_manaSystem.HasEnoughMana(GetEffectiveManaCost(card)))
                return CardPlayFailure.NotEnoughMana;
            if (requireTarget && card.ManualTargetEffect != null &&
                (_targetResolver == null || !_targetResolver.isActiveAndEnabled ||
                    !_targetResolver.IsValidTarget(card, target)))
                return CardPlayFailure.InvalidTarget;
            return CardPlayFailure.None;
        }

        public bool TryResolveTarget(Card card, Vector2 screenPosition, out CombatantView target)
        {
            target = null;
            return _targetResolver != null && _targetResolver.TryResolve(card, screenPosition, out target);
        }

        public bool TryPlay(Card card, CombatantView target, out CardPlayFailure failure)
        {
            failure = GetFailure(card, target);
            if (failure != CardPlayFailure.None) return false;

            int manaCost = card.IsXCost ? _manaSystem.AvailableMana : GetEffectiveManaCost(card);
            // Immediately detach card from hand, reserve mana, and launch non-blocking exit tween
            // so the hand fans shut on frame 0 and the next card can be hovered/played instantaneously!
            _manaSystem.ReserveMana(manaCost);
            _cardSystem.ReserveCardPlay(card);

            PlayCardGA playAction = card.IsXCost
                ? new PlayCardGA(card, target, manaCost)
                : new PlayCardGA(card, target);

            if (_actionSystem.Enqueue(playAction))
            {
                RefreshState();
                return true;
            }

            _manaSystem.ReleaseReservedMana(manaCost);
            failure = CardPlayFailure.Busy;
            return false;
        }

        public void RefreshState() => StateChanged?.Invoke();
        private void OnBusyChanged(bool busy) => RefreshState();
        private void OnManaChanged(int _, int __) => RefreshState();
    }
}
