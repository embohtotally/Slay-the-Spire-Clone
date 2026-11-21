using System.Collections.Generic;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    public class MultiHitDamageEffect : Effect
    {
        [SerializeField, Min(0)] private int damagePerHit = 4;
        [SerializeField, Min(1)] private int hitCount = 2;
        [SerializeField] private bool scaleHitsWithX = false;

        public int DamagePerHit => damagePerHit;
        public int HitCount => Mathf.Max(1, hitCount);
        public bool ScaleHitsWithX => scaleHitsWithX;

        public int ResolveHitCount(int xMultiplier)
        {
            if (xMultiplier <= 0) return 0;
            return scaleHitsWithX ? Mathf.Max(0, HitCount * xMultiplier) : HitCount;
        }

        public int ResolveDamagePerHit(int xMultiplier)
        {
            if (xMultiplier <= 0) return 0;
            return scaleHitsWithX ? damagePerHit : damagePerHit * xMultiplier;
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return GetGameAction(targets, caster, 1);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            int hits = ResolveHitCount(xMultiplier);
            int perHit = ResolveDamagePerHit(xMultiplier);
            if (hits <= 0 || perHit <= 0) return null;

            List<GameAction> actions = new(hits);
            for (int i = 0; i < hits; i++)
            {
                actions.Add(new DealDamageGA(perHit, targets != null ? new List<CombatantView>(targets) : new List<CombatantView>(), caster));
            }
            return actions.Count == 1 ? actions[0] : new SequentialGameAction(actions);
        }
    }
}

