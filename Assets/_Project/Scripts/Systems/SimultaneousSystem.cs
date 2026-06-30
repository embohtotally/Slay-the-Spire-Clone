using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SimultaneousSystem : Singleton<SimultaneousSystem>
{
    private void OnEnable()
    {
        ActionSystem.AttachPerformer<SimultaneousGA>(SimultaneousPerformer);
        ActionSystem.AttachPerformer<SequentialGameAction>(SequentialPerformer);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<SimultaneousGA>();
        ActionSystem.DetachPerformer<SequentialGameAction>();
    }

    private IEnumerator SimultaneousPerformer(SimultaneousGA simultaneousGA)
    {
        if (simultaneousGA.Actions != null)
        {
            foreach (GameAction action in simultaneousGA.Actions)
            {
                ActionSystem.Instance.AddReaction(action);
            }
        }
        yield return null;
    }

    private IEnumerator SequentialPerformer(SequentialGameAction sequentialGA)
    {
        if (sequentialGA.Actions != null)
        {
            foreach (GameAction action in sequentialGA.Actions)
            {
                ActionSystem.Instance.AddReaction(action);
            }
        }
        yield return null;
    }
}
