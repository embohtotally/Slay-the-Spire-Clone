using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Tutorials;
using UnityEngine;

namespace PlushieOdyssey.UI
{
    public class EndTurnButtonUI : MonoBehaviour
    {
        [SerializeField] private global::PlushieOdyssey.Audio.SfxID endTurnSfx = global::PlushieOdyssey.Audio.SfxID.EndTurn;

        public void OnClick()
        {
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanEndTurn()) return;
            if (ActionSystem.Instance == null) return;
            if (ActionSystem.Instance.IsEnemyTurnQueuedOrActive) return;
            if (EnemySystem.Instance != null && EnemySystem.Instance.IsEnemyTurnActive) return;

            if (endTurnSfx != global::PlushieOdyssey.Audio.SfxID.None)
                global::PlushieOdyssey.Audio.Tune.SFX(endTurnSfx);

            // Queue FIFO behind any in-flight card actions so End Turn is NEVER ignored!
            ActionSystem.Instance.Enqueue(new EnemyTurnGA());
        }
    }
}
