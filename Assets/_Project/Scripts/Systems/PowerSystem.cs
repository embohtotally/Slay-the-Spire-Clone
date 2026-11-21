using System;
using System.Collections.Generic;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.Data;
using PlushieOdyssey.UI.Combat;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    /// <summary>
    /// Tracks played CardType.Power cards for the remainder of combat, removes them from the deck cycle,
    /// triggers persistent passives (TurnStart, TurnEnd, OnAttackCardPlayed, OnBuffCardPlayed,
    /// OnAnyCardPlayed, OnEnterHighWill, OnEnterLowWill), and exposes active Power badges for the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public class PowerSystem : Singleton<PowerSystem>
    {
        public sealed class ActivePowerEntry
        {
            public string PassiveId;
            public string DisplayName;
            public string Description;
            public PowerPassiveTrigger Trigger;
            public Color BadgeColor;
            public List<AutoTargetEffect> Effects;
            public int Stacks;
        }

        private readonly List<ActivePowerEntry> activePowers = new();
        private HeroView subscribedHero;
        private bool wasHighWill;
        private bool wasLowWill;
        private Card justActivatedPowerCard;

        public IReadOnlyList<ActivePowerEntry> ActivePowers => activePowers;
        public event Action PowersChanged;

        private void OnEnable()
        {
            ActionSystem.SubscribeReaction<CombatStartedGA>(OnCombatStartedPost, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<CombatWonGA>(OnCombatWonPost, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPre, ReactionTiming.PRE);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<PlayCardGA>(OnPlayCardPost, ReactionTiming.POST);
            TrySubscribeHero();
        }

        private void OnDisable()
        {
            ActionSystem.UnsubscribeReaction<CombatStartedGA>(OnCombatStartedPost, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<CombatWonGA>(OnCombatWonPost, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPre, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<PlayCardGA>(OnPlayCardPost, ReactionTiming.POST);
            UnsubscribeHero();
        }

        private void Update()
        {
            if (subscribedHero == null && HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null)
            {
                TrySubscribeHero();
            }
        }

        public void ClearAllPowers()
        {
            if (activePowers.Count == 0) return;
            activePowers.Clear();
            PowersChanged?.Invoke();
        }

        public void RegisterPowerCard(Card card)
        {
            if (card == null || !card.IsPersistentPower) return;

            justActivatedPowerCard = card;

            if (card.PowerPassives != null && card.PowerPassives.Count > 0)
            {
                for (int i = 0; i < card.PowerPassives.Count; i++)
                {
                    PowerPassiveDefinition def = card.PowerPassives[i];
                    if (def == null) continue;
                    string id = !string.IsNullOrWhiteSpace(def.PassiveId) ? def.PassiveId.Trim() : $"{card.Title}_{i}";
                    AddOrStackPower(
                        id,
                        string.IsNullOrWhiteSpace(def.DisplayName) ? card.Title : def.DisplayName.Trim(),
                        string.IsNullOrWhiteSpace(def.Description) ? card.Description : def.Description.Trim(),
                        def.Trigger,
                        def.BadgeColor.a > 0.05f ? def.BadgeColor : new Color(0.98f, 0.80f, 0.36f, 1f),
                        def.Effects);
                }
            }
            else
            {
                AddOrStackPower(
                    card.Title,
                    card.Title,
                    string.IsNullOrWhiteSpace(card.Description) ? "Persistent combat Power." : card.Description,
                    PowerPassiveTrigger.TurnStart,
                    new Color(0.98f, 0.80f, 0.36f, 1f),
                    null);
            }

            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            if (hero != null)
            {
                GameManager.GenerateFloatingText($"POWER: {card.Title}", hero.transform, 1.15f, 1.05f, "#FFD54F", 1.12f);
            }

            CombatLogUI.Log($"<color=#FFD54F><b>Power Activated:</b></color> {card.Title}");
            PowersChanged?.Invoke();
        }

        public List<CombatStatusBadgeData> CollectPowerBadges()
        {
            var results = new List<CombatStatusBadgeData>(activePowers.Count);
            for (int i = 0; i < activePowers.Count; i++)
            {
                ActivePowerEntry entry = activePowers[i];
                if (entry == null) continue;
                results.Add(CombatStatusBadgeData.CreatePowerBadge(
                    entry.PassiveId,
                    entry.DisplayName,
                    entry.Description,
                    entry.Stacks,
                    entry.BadgeColor,
                    entry.Trigger));
            }
            return results;
        }

        private void AddOrStackPower(
            string id,
            string displayName,
            string description,
            PowerPassiveTrigger trigger,
            Color badgeColor,
            List<AutoTargetEffect> effects)
        {
            for (int i = 0; i < activePowers.Count; i++)
            {
                if (string.Equals(activePowers[i].PassiveId, id, StringComparison.OrdinalIgnoreCase))
                {
                    activePowers[i].Stacks++;
                    return;
                }
            }

            activePowers.Add(new ActivePowerEntry
            {
                PassiveId = id,
                DisplayName = displayName,
                Description = description,
                Trigger = trigger,
                BadgeColor = badgeColor,
                Effects = effects != null ? new List<AutoTargetEffect>(effects) : new List<AutoTargetEffect>(),
                Stacks = 1
            });
        }

        private void OnCombatStartedPost(CombatStartedGA _)
        {
            activePowers.Clear();
            justActivatedPowerCard = null;
            TrySubscribeHero();
            SyncHeroWillFlags();
            PowersChanged?.Invoke();
        }

        private void OnCombatWonPost(CombatWonGA _)
        {
            ClearAllPowers();
        }

        private void OnEnemyTurnPre(EnemyTurnGA _)
        {
            TriggerPassives(PowerPassiveTrigger.TurnEnd, addAsReaction: true);
        }

        private void OnEnemyTurnPost(EnemyTurnGA _)
        {
            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            if (hero == null || hero.CurrentHealth <= 0 || hero.IsStunned) return;
            TriggerPassives(PowerPassiveTrigger.TurnStart, addAsReaction: true);
        }

        private void OnPlayCardPost(PlayCardGA playCardGA)
        {
            if (playCardGA?.Card == null) return;
            if (playCardGA.Card == justActivatedPowerCard)
            {
                justActivatedPowerCard = null;
                return;
            }

            if (playCardGA.Card.Type == CardType.Attack)
            {
                TriggerPassives(PowerPassiveTrigger.OnAttackCardPlayed, addAsReaction: true);
            }
            else if (playCardGA.Card.Type == CardType.Buff)
            {
                TriggerPassives(PowerPassiveTrigger.OnBuffCardPlayed, addAsReaction: true);
            }

            TriggerPassives(PowerPassiveTrigger.OnAnyCardPlayed, addAsReaction: true);
        }

        private void HandleHeroWillChanged()
        {
            if (subscribedHero == null) return;
            bool isHigh = subscribedHero.IsHighWill;
            bool isLow = subscribedHero.IsLowWill;

            if (isHigh && !wasHighWill)
            {
                TriggerPassives(PowerPassiveTrigger.OnEnterHighWill, addAsReaction: ActionSystem.Instance != null && ActionSystem.Instance.IsPerforming);
            }
            if (isLow && !wasLowWill)
            {
                TriggerPassives(PowerPassiveTrigger.OnEnterLowWill, addAsReaction: ActionSystem.Instance != null && ActionSystem.Instance.IsPerforming);
            }

            wasHighWill = isHigh;
            wasLowWill = isLow;
        }

        private void TriggerPassives(PowerPassiveTrigger trigger, bool addAsReaction)
        {
            if (activePowers.Count == 0 || ActionSystem.Instance == null) return;
            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            if (hero == null || hero.CurrentHealth <= 0) return;

            for (int i = 0; i < activePowers.Count; i++)
            {
                ActivePowerEntry entry = activePowers[i];
                if (entry == null || entry.Trigger != trigger || entry.Effects == null || entry.Effects.Count == 0)
                    continue;

                int repeat = Mathf.Max(1, entry.Stacks);
                for (int s = 0; s < repeat; s++)
                {
                    for (int e = 0; e < entry.Effects.Count; e++)
                    {
                        AutoTargetEffect wrapper = entry.Effects[e];
                        if (wrapper?.Effect == null || wrapper.TargetMode == null) continue;
                        List<CombatantView> targets = wrapper.TargetMode.GetTargets(hero);
                        PerformEffectGA action = new(wrapper.Effect, targets, hero, xMultiplier: 1);
                        if (addAsReaction) ActionSystem.Instance.AddReaction(action);
                        else ActionSystem.Instance.Enqueue(action);
                    }
                }

                CombatLogUI.Log($"<color=#FFD54F>[Power]</color> <b>{entry.DisplayName}</b> triggered{(repeat > 1 ? $" (×{repeat})" : string.Empty)}.");
            }
        }

        private void TrySubscribeHero()
        {
            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            if (hero == null || hero == subscribedHero) return;
            UnsubscribeHero();
            subscribedHero = hero;
            subscribedHero.StressStateChanged += HandleHeroWillChanged;
            SyncHeroWillFlags();
        }

        private void UnsubscribeHero()
        {
            if (subscribedHero != null)
            {
                subscribedHero.StressStateChanged -= HandleHeroWillChanged;
                subscribedHero = null;
            }
        }

        private void SyncHeroWillFlags()
        {
            wasHighWill = subscribedHero != null && subscribedHero.IsHighWill;
            wasLowWill = subscribedHero != null && subscribedHero.IsLowWill;
        }
    }
}

