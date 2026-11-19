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
            CombatantView caster = performEffectGA.Caster != null
                ? performEffectGA.Caster
                : (HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
            GameAction effectAction = performEffectGA.Effect.GetGameAction(performEffectGA.Targets, caster);
            ActionSystem.Instance.AddReaction(effectAction);
            yield return null;
        }
    #endregion
    }
}
