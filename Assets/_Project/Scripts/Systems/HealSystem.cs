using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using System.Collections;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [System.Serializable]
    public class HealSystem : MonoBehaviour
    {

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
                GameManager.GenerateFloatingText("+" + healGA.Amount, target.transform, 1f, 1f, "#00FF00");
            }

            yield return new WaitForSeconds(0.4f);
        }
    }
}
