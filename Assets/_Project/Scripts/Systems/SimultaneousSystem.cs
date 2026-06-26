using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SimultaneousSystem : Singleton<SimultaneousSystem>
{
    private void OnEnable()
    {
        ActionSystem.AttachPerformer<SimultaneousGA>(SimultaneousPerformer);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<SimultaneousGA>();
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
}
