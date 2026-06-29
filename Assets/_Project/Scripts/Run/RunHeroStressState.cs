using System;
using UnityEngine;

[Serializable]
public class RunHeroStressState
{
    [field: SerializeField] public HeroData HeroData { get; private set; }
    [field: SerializeField] public int CurrentStress { get; private set; }
    [field: SerializeField] public int MaxStress { get; private set; }

    public bool IsStressed => CurrentStress >= MaxStress;

    public RunHeroStressState(HeroData heroData, int maxStress, int currentStress = 0)
    {
        HeroData = heroData;
        MaxStress = Mathf.Max(1, maxStress);
        CurrentStress = Mathf.Clamp(currentStress, 0, MaxStress);
    }

    public RunHeroStressState(RunHeroStressState source)
    {
        HeroData = source?.HeroData;
        MaxStress = Mathf.Max(1, source?.MaxStress ?? 1);
        CurrentStress = Mathf.Clamp(source?.CurrentStress ?? 0, 0, MaxStress);
    }

    public void SetStress(int amount)
    {
        CurrentStress = Mathf.Clamp(amount, 0, MaxStress);
    }

    public void AddStress(int amount)
    {
        if (amount <= 0 || IsStressed) return;
        SetStress(CurrentStress + amount);
    }

    public void ReduceStress(int amount)
    {
        if (amount <= 0) return;
        SetStress(CurrentStress - amount);
    }

    public void ClearStress()
    {
        CurrentStress = 0;
    }

    public void ChangeMaxStress(int amount)
    {
        if (amount == 0) return;
        MaxStress = Mathf.Max(1, MaxStress + amount);
        CurrentStress = Mathf.Clamp(CurrentStress, 0, MaxStress);
    }
}
