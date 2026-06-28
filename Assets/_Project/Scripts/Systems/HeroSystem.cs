using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HeroSystem : Singleton<HeroSystem>
{
    [field: SerializeField] public HeroView HeroView { get; private set; }

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<AddStressGA>(AddStressPerformer);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<AddStressGA>();
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
        }
        yield return null;
    }

}