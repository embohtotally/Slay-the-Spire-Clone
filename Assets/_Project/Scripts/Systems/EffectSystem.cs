using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [System.Serializable]
    public class EffectSystem : MonoBehaviour
    {
        private void OnEnable()
        {
            ActionSystem.AttachPerformer<PerformEffectGA>(PerformEffectPerformer);
        }

        private void OnDisable()
        {
            ActionSystem.DetachPerformer<PerformEffectGA>();
        }

    #region Performers
        private IEnumerator PerformEffectPerformer(PerformEffectGA performEffectGA)
        {
            if (performEffectGA?.Effect == null) yield break;

            CombatantView caster = performEffectGA.Caster != null
                ? performEffectGA.Caster
                : (HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
            GameAction effectAction = performEffectGA.Effect.GetGameAction(
                performEffectGA.Targets,
                caster,
                performEffectGA.XMultiplier);
            if (effectAction != null)
            {
                if (performEffectGA.PotencyBonus > 0)
                {
                    ApplyPotencyBonus(effectAction, performEffectGA.PotencyBonus);
                }
                ActionSystem.Instance.AddReaction(effectAction);
            }
            yield return null;
        }

        private static void ApplyPotencyBonus(GameAction action, int bonus)
        {
            switch (action)
            {
                case DealDamageGA dmg when !dmg.IsThornsDamage && dmg.Amount > 0:
                    dmg.Amount += bonus;
                    break;
                case GainShieldGA shield when shield.Amount > 0:
                    shield.Amount += bonus;
                    break;
                case SequentialGameAction seq when seq.Actions != null:
                    for (int i = 0; i < seq.Actions.Count; i++) ApplyPotencyBonus(seq.Actions[i], bonus);
                    break;
                case SimultaneousGA sim when sim.Actions != null:
                    for (int i = 0; i < sim.Actions.Count; i++) ApplyPotencyBonus(sim.Actions[i], bonus);
                    break;
            }
        }
    #endregion
    }
}
