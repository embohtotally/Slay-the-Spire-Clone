using System.Collections;
using UnityEngine;

[System.Serializable]
public class TauntSystem : MonoBehaviour
{
    private void OnEnable()
    {
        ActionSystem.AttachPerformer<ApplyTauntGA>(ApplyTauntPerformer);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<ApplyTauntGA>();
    }

    private IEnumerator ApplyTauntPerformer(ApplyTauntGA applyTauntGA)
    {
        foreach (CombatantView target in applyTauntGA.Targets)
        {
            target.ApplyTaunt(applyTauntGA.Duration);
        }
        
        yield return null;
    }
}
