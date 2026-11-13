using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Tutorials;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.UI
{
    public class EndTurnButtonUI : MonoBehaviour
    {
        [SerializeField] private global::PlushieOdyssey.Audio.SfxID endTurnSfx = global::PlushieOdyssey.Audio.SfxID.EndTurn;

        public void OnClick()
        {
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanEndTurn()) return;

            if (endTurnSfx != global::PlushieOdyssey.Audio.SfxID.None) global::PlushieOdyssey.Audio.Tune.SFX(endTurnSfx);
            EnemyTurnGA enemyTurnGA = new();
            ActionSystem.Instance.Perform(enemyTurnGA);
        }
    }
}
