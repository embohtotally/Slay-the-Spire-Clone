using System.Collections.Generic;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    public class HandSizeDamageEffect : Effect
    {
        [SerializeField, Min(1)] private int damagePerCardInHand = 1;

        public int DamagePerCardInHand => Mathf.Max(1, damagePerCardInHand);

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            int handCount = CardSystem.Instance != null ? Mathf.Max(1, CardSystem.Instance.CurrentEndTurnHandCount) : 1;
            int dmg = handCount * DamagePerCardInHand;
            return new DealDamageGA(dmg, targets, caster, isThornsDamage: true);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            int handCount = CardSystem.Instance != null ? Mathf.Max(1, CardSystem.Instance.CurrentEndTurnHandCount) : 1;
            int dmg = handCount * DamagePerCardInHand * xMultiplier;
            return new DealDamageGA(dmg, targets, caster, isThornsDamage: true);
        }
    }
}

