using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [System.Serializable]
    public class HeroSystem : Singleton<HeroSystem>
    {
        [field: SerializeField] public HeroView HeroView { get; private set; }

        private void OnEnable()
        {
            ActionSystem.AttachPerformer<AddStressGA>(AddStressPerformer);
            ActionSystem.AttachPerformer<ModifyStressGA>(ModifyStressPerformer);
            ActionSystem.AttachPerformer<SetStressGA>(SetStressPerformer);
        }

        private void OnDisable()
        {
            ActionSystem.DetachPerformer<AddStressGA>();
            ActionSystem.DetachPerformer<ModifyStressGA>();
            ActionSystem.DetachPerformer<SetStressGA>();
        }

        public void Setup(List<HeroData> heroTeam)
        {
            HeroView.Setup(heroTeam);
        }

        private IEnumerator AddStressPerformer(AddStressGA addStressGA)
        {
            foreach (CombatantView target in addStressGA.Targets)
            {
                target.AddStress(addStressGA.Amount);
                GameManager.GenerateFloatingText("+" + addStressGA.Amount + " Fear", target.transform, 1f, 1f, "#800080");
                target.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.2f, 5, 1f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator ModifyStressPerformer(ModifyStressGA modifyStressGA)
        {
            foreach (CombatantView target in modifyStressGA.Targets)
            {
                if (target is HeroView heroView)
                {
                    if (modifyStressGA.Amount > 0)
                    {
                        heroView.AddStressToAllHeroes(modifyStressGA.Amount);
                        GameManager.GenerateFloatingText("+" + modifyStressGA.Amount + " Fear", target.transform, 1f, 1f, "#800080");
                        target.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.2f, 5, 1f);
                    }
                    else if (modifyStressGA.Amount < 0)
                    {
                        heroView.ReduceStressToAllHeroes(-modifyStressGA.Amount);
                        GameManager.GenerateFloatingText(modifyStressGA.Amount + " Fear", target.transform, 1f, 1f, "#00FF00");
                        target.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.2f, 5, 1f);
                    }
                }
            }
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator SetStressPerformer(SetStressGA setStressGA)
        {
            int sourceStress = 0;
            if (HeroView != null)
            {
                for (int i = 0; i < HeroView.HeroTeam.Count; i++)
                {
                    if (HeroView.HeroTeam[i] != null && HeroView.HeroTeam[i].name.Contains(setStressGA.SourceAllyName))
                    {
                        sourceStress = HeroView.GetStressStateCopies()[i].CurrentStress;
                        break;
                    }
                }
            }

            foreach (CombatantView target in setStressGA.Targets)
            {
                if (target is HeroView heroView)
                {
                    heroView.SetCurrentStress(sourceStress);
                    GameManager.GenerateFloatingText("Fear = " + sourceStress, target.transform, 1f, 1f, "#800080");
                    target.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.2f, 5, 1f);
                }
            }
            yield return new WaitForSeconds(0.4f);
        }
    }
}
