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
    /// Executes Enemy Passive Traits during combat:
    /// Spiky (Thorns), Regenerate, Ritual, Enrage, Time Warp, Mode Shift, and Will-Eater Aura.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyPassiveSystem : Singleton<EnemyPassiveSystem>
    {
        public event Action EnemyPassivesChanged;

        private void OnEnable()
        {
            ActionSystem.SubscribeReaction<DealDamageGA>(OnDealDamagePost, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<PlayCardGA>(OnPlayCardPost, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);
        }

        private void OnDisable()
        {
            ActionSystem.UnsubscribeReaction<DealDamageGA>(OnDealDamagePost, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<PlayCardGA>(OnPlayCardPost, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);
        }

        private void OnDealDamagePost(DealDamageGA dealDamageGA)
        {
            if (dealDamageGA == null || dealDamageGA.Targets == null || dealDamageGA.Amount <= 0) return;
            bool changed = false;

            for (int i = 0; i < dealDamageGA.Targets.Count; i++)
            {
                if (dealDamageGA.Targets[i] is not EnemyView enemy || enemy.Data == null) continue;
                List<EnemyPassiveTrait> traits = enemy.Data.PassiveTraits;
                if (traits == null || traits.Count == 0) continue;

                for (int t = 0; t < traits.Count; t++)
                {
                    EnemyPassiveTrait trait = traits[t];
                    if (trait == null) continue;

                    if (trait.PassiveType == EnemyPassiveType.Spiky && !dealDamageGA.IsThornsDamage &&
                        dealDamageGA.Caster != null && dealDamageGA.Caster != enemy && dealDamageGA.Caster.CurrentHealth > 0)
                    {
                        int thornsDmg = Mathf.Max(1, trait.Value);
                        ActionSystem.Instance.AddReaction(new DealDamageGA(
                            thornsDmg, new List<CombatantView> { dealDamageGA.Caster }, enemy, isThornsDamage: true));
                        GameManager.GenerateFloatingText($"Spiky -{thornsDmg}", dealDamageGA.Caster.transform, 0.9f, 1.0f, "#FFAB40");
                        CombatLogUI.Log($"<color=#FFAB40>[Passive]</color> <b>{enemy.Data.name}</b>'s Spiky reflected {thornsDmg} damage.");
                    }
                    else if (trait.PassiveType == EnemyPassiveType.ModeShift && enemy.CurrentHealth > 0)
                    {
                        if (enemy.RecordModeShiftDamage(dealDamageGA.Amount, out int shieldToGain))
                        {
                            ActionSystem.Instance.AddReaction(new GainShieldGA(
                                shieldToGain, new List<CombatantView> { enemy }, enemy));
                            GameManager.GenerateFloatingText($"MODE SHIFT! +{shieldToGain} Shield", enemy.transform, 1.15f, 1.15f, "#56D0FF", 1.15f);
                            CombatLogUI.Log($"<color=#56D0FF>[Passive]</color> <b>{enemy.Data.name}</b> triggered Mode Shift (+{shieldToGain} Shield)!");
                        }
                        changed = true;
                    }
                }
            }

            if (changed) EnemyPassivesChanged?.Invoke();
        }

        private void OnPlayCardPost(PlayCardGA playCardGA)
        {
            if (playCardGA?.Card == null || EnemySystem.Instance == null || EnemySystem.Instance.Enemies == null) return;

            bool changed = false;
            bool forceEndTurn = false;

            for (int i = 0; i < EnemySystem.Instance.Enemies.Count; i++)
            {
                EnemyView enemy = EnemySystem.Instance.Enemies[i];
                if (enemy == null || enemy.CurrentHealth <= 0 || enemy.Data?.PassiveTraits == null) continue;

                foreach (EnemyPassiveTrait trait in enemy.Data.PassiveTraits)
                {
                    if (trait == null) continue;

                    if (trait.PassiveType == EnemyPassiveType.Enrage && playCardGA.Card.Type == CardType.Buff)
                    {
                        int str = Mathf.Max(1, trait.Value);
                        ActionSystem.Instance.AddReaction(new ApplyBuffGA(
                            new List<CombatantView> { enemy }, BuffType.Strength, str, 999, enemy));
                        GameManager.GenerateFloatingText($"ENRAGE +{str} STR!", enemy.transform, 1.05f, 1.05f, "#FF6E40");
                        CombatLogUI.Log($"<color=#FF6E40>[Passive]</color> <b>{enemy.Data.name}</b> Enraged (+{str} Strength)!");
                    }
                    else if (trait.PassiveType == EnemyPassiveType.TimeWarp)
                    {
                        int limit = Mathf.Max(2, trait.Value);
                        if (enemy.IncrementTimeWarp(limit))
                        {
                            int bonusStr = Mathf.Max(1, trait.SecondaryValue > 0 ? trait.SecondaryValue : 2);
                            ActionSystem.Instance.AddReaction(new ApplyBuffGA(
                                new List<CombatantView> { enemy }, BuffType.Strength, bonusStr, 999, enemy));
                            GameManager.GenerateFloatingText($"TIME WARP! +{bonusStr} STR", enemy.transform, 1.25f, 1.2f, "#CE93D8", 1.18f);
                            CombatLogUI.Log($"<color=#CE93D8>[Passive]</color> <b>{enemy.Data.name}</b> triggered Time Warp (+{bonusStr} Strength & ends your turn)!");
                            forceEndTurn = true;
                        }
                        changed = true;
                    }
                }
            }

            if (changed) EnemyPassivesChanged?.Invoke();

            if (forceEndTurn && ActionSystem.Instance != null && !ActionSystem.Instance.IsEnemyTurnQueuedOrActive)
            {
                ActionSystem.Instance.Enqueue(new EnemyTurnGA());
            }
        }

        private void OnEnemyTurnPost(EnemyTurnGA _)
        {
            if (EnemySystem.Instance == null || EnemySystem.Instance.Enemies == null) return;
            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            if (hero == null || hero.CurrentHealth <= 0) return;

            for (int i = 0; i < EnemySystem.Instance.Enemies.Count; i++)
            {
                EnemyView enemy = EnemySystem.Instance.Enemies[i];
                if (enemy == null || enemy.CurrentHealth <= 0 || enemy.Data?.PassiveTraits == null) continue;

                foreach (EnemyPassiveTrait trait in enemy.Data.PassiveTraits)
                {
                    if (trait == null) continue;

                    switch (trait.PassiveType)
                    {
                        case EnemyPassiveType.Regenerate:
                            int heal = Mathf.Max(1, trait.Value);
                            if (enemy.CurrentHealth < enemy.EffectiveMaxHealth)
                            {
                                ActionSystem.Instance.AddReaction(new HealGA(heal, new List<CombatantView> { enemy }, enemy));
                                CombatLogUI.Log($"<color=#59E892>[Passive]</color> <b>{enemy.Data.name}</b> Regenerated +{heal} HP.");
                            }
                            break;

                        case EnemyPassiveType.Ritual:
                            int str = Mathf.Max(1, trait.Value);
                            ActionSystem.Instance.AddReaction(new ApplyBuffGA(
                                new List<CombatantView> { enemy }, BuffType.Strength, str, 999, enemy));
                            GameManager.GenerateFloatingText($"Ritual +{str} STR", enemy.transform, 1.0f, 1.0f, "#FFD54F");
                            CombatLogUI.Log($"<color=#FFD54F>[Passive]</color> <b>{enemy.Data.name}</b> performed Ritual (+{str} Strength).");
                            break;

                        case EnemyPassiveType.WillEaterAura:
                            int drain = Mathf.Max(1, trait.Value);
                            ActionSystem.Instance.AddReaction(new AddStressGA(drain, new List<CombatantView> { hero }, enemy));
                            CombatLogUI.Log($"<color=#CE93D8>[Passive]</color> <b>{enemy.Data.name}</b>'s Will-Eater Aura drained -{drain} Will.");
                            break;
                    }
                }
            }
        }
    }
}

