using System.Collections;
using UnityEngine;

[System.Serializable]
public class HealSystem : MonoBehaviour
{
    [SerializeField] private DamagePopupView popupPrefab;

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<HealGA>(HealPerformer);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<HealGA>();
    }

    private IEnumerator HealPerformer(HealGA healGA)
    {
        foreach (CombatantView target in healGA.Targets)
        {
            target.Heal(healGA.Amount);
            if (popupPrefab != null)
            {
                DamagePopupView popup = Instantiate(popupPrefab);
                popup.Setup("+" + healGA.Amount, target.transform.position, Color.green);
            }
        }
        
        yield return null;
    }
}
