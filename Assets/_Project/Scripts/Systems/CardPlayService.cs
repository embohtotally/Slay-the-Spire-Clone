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

        public bool IsBusy => _actionSystem != null && _actionSystem.IsPerforming;
        public event Action StateChanged;

        private void OnEnable()
        {
            if (_actionSystem != null) _actionSystem.BusyChanged += OnBusyChanged;
            if (_heroView != null) _heroView.StatusEffectsChanged += RefreshState;
        }

        private void OnDisable()
        {
            if (_actionSystem != null) _actionSystem.BusyChanged -= OnBusyChanged;
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
            if (IsBusy) return CardPlayFailure.Busy;
            if (_heroView.CurrentHealth <= 0 ||
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
            if (_actionSystem.Perform(new PlayCardGA(card, target))) return true;
            failure = CardPlayFailure.Busy;
            return false;
        }

        public void RefreshState() => StateChanged?.Invoke();
        private void OnBusyChanged(bool busy) => RefreshState();
    }
}
