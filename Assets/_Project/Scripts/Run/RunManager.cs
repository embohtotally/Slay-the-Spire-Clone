using System;
using System.Collections.Generic;
using UnityEngine;

public class RunManager : PersistentSingleton<RunManager>
{
    [Header("Run Defaults")]
    [SerializeField] private int startingGold;

    private readonly List<RunHeroStressState> heroStressStates = new();

    public MapGraph CurrentMap { get; private set; }
    public string CurrentMapNodeId { get; private set; }
    public EncounterData SelectedEncounter { get; private set; }
    public bool HasActiveRun => CurrentMap != null;

    public bool HasHeroState { get; private set; }
    public int HeroCurrentHealth { get; private set; }
    public int HeroMaxHealth { get; private set; }
    public int HeroCurrentStress { get; private set; }
    public int HeroMaxStress { get; private set; }
    public IReadOnlyList<RunHeroStressState> HeroStressStates => heroStressStates;
    public int Gold { get; private set; }

    public Dictionary<string, int> Counters { get; private set; } = new();
    public Dictionary<string, int> CardCostModifiers { get; private set; } = new();

    public event Action RunStateChanged;

    // Backward-compatible access for older scripts that only cared about current HP.
    public int? CurrentHeroHealth
    {
        get => HasHeroState ? HeroCurrentHealth : null;
        set
        {
            if (!value.HasValue)
            {
                ClearHeroState();
                NotifyRunStateChanged();
                return;
            }

            if (!HasHeroState)
            {
                InitializeHeroState(Mathf.Max(1, value.Value), HeroMaxStress > 0 ? HeroMaxStress : 100);
            }

            SetHeroHealth(value.Value);
        }
    }

    public void StartNewRun(MapGraph mapGraph)
    {
        CurrentMap = mapGraph;
        CurrentMapNodeId = null;
        SelectedEncounter = null;
        ClearHeroState();
        Gold = Mathf.Max(0, startingGold);
        Counters.Clear();
        CardCostModifiers.Clear();

        if (RunDeckManager.Instance != null)
        {
            RunDeckManager.Instance.ClearDeck();
        }

        if (RunPotionManager.Instance != null)
        {
            RunPotionManager.Instance.ClearPotions();
        }

        NotifyRunStateChanged();
    }

    public void SelectMapNode(MapNode selectedNode)
    {
        if (selectedNode == null) return;

        CurrentMapNodeId = selectedNode.Id;
        SelectedEncounter = selectedNode.Encounter;
        NotifyRunStateChanged();
    }

    public void ClearSelectedEncounter()
    {
        SelectedEncounter = null;
        NotifyRunStateChanged();
    }

    public void CompleteCurrentEncounter()
    {
        ClearSelectedEncounter();
        if (HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null)
        {
            CaptureHeroState(HeroSystem.Instance.HeroView);
        }
    }

    public void InitializeHeroState(int maxHealth, int maxStress)
    {
        maxHealth = Mathf.Max(1, maxHealth);
        maxStress = Mathf.Max(1, maxStress);

        if (HasHeroState)
        {
            return;
        }

        HasHeroState = true;
        HeroMaxHealth = maxHealth;
        HeroCurrentHealth = maxHealth;
        heroStressStates.Clear();
        heroStressStates.Add(new RunHeroStressState(null, maxStress));
        RefreshAggregateStress();
        NotifyRunStateChanged();
    }

    public void InitializeHeroState(IReadOnlyList<HeroData> heroTeam, int maxStress)
    {
        int maxHealth = GetTotalHeroHealth(heroTeam);
        if (maxHealth <= 0) return;

        if (HasHeroState)
        {
            int validHeroCount = CountValidHeroes(heroTeam);
            if (heroStressStates.Count == Mathf.Max(1, validHeroCount)) return;

            RebuildHeroStressStates(heroTeam, maxStress, HeroCurrentStress);
            HeroMaxHealth = Mathf.Max(1, maxHealth);
            HeroCurrentHealth = Mathf.Clamp(HeroCurrentHealth, 0, HeroMaxHealth);
            RefreshAggregateStress();
            NotifyRunStateChanged();
            return;
        }

        HasHeroState = true;
        HeroMaxHealth = maxHealth;
        HeroCurrentHealth = maxHealth;
        RebuildHeroStressStates(heroTeam, maxStress, 0);
        RefreshAggregateStress();
        NotifyRunStateChanged();
    }

    public void CaptureHeroState(HeroView heroView)
    {
        if (heroView == null) return;

        InitializeHeroState(heroView.HeroTeam, heroView.MaxStress);
        HeroMaxHealth = Mathf.Max(1, heroView.MaxHealth);
        HeroCurrentHealth = Mathf.Clamp(heroView.CurrentHealth, 0, HeroMaxHealth);

        heroStressStates.Clear();
        foreach (RunHeroStressState stressState in heroView.GetStressStateCopies())
        {
            heroStressStates.Add(stressState);
        }

        if (heroStressStates.Count == 0)
        {
            heroStressStates.Add(new RunHeroStressState(null, Mathf.Max(1, heroView.MaxStress), heroView.CurrentStress));
        }

        RefreshAggregateStress();
        NotifyRunStateChanged();
    }

    public List<RunHeroStressState> GetHeroStressStateCopies()
    {
        List<RunHeroStressState> copies = new();
        foreach (RunHeroStressState stressState in heroStressStates)
        {
            copies.Add(new RunHeroStressState(stressState));
        }

        return copies;
    }

    public void RestorePlayerState(
        int currentHealth,
        int maxHealth,
        IReadOnlyList<RunHeroStressState> restoredStressStates,
        int goldAmount)
    {
        HeroMaxHealth = Mathf.Max(1, maxHealth);
        HeroCurrentHealth = Mathf.Clamp(currentHealth, 0, HeroMaxHealth);
        HasHeroState = true;

        heroStressStates.Clear();
        if (restoredStressStates != null)
        {
            foreach (RunHeroStressState stressState in restoredStressStates)
            {
                if (stressState != null)
                {
                    heroStressStates.Add(new RunHeroStressState(stressState));
                }
            }
        }

        if (heroStressStates.Count == 0)
        {
            heroStressStates.Add(new RunHeroStressState(null, HeroMaxStress > 0 ? HeroMaxStress : 100));
        }

        Gold = Mathf.Max(0, goldAmount);
        RefreshAggregateStress();
        NotifyRunStateChanged();
    }

    public void SetHeroHealth(int amount)
    {
        if (!HasHeroState) return;

        HeroCurrentHealth = Mathf.Clamp(amount, 0, HeroMaxHealth);
        NotifyRunStateChanged();
    }

    public void HealHero(int amount)
    {
        if (!HasHeroState || amount <= 0) return;
        SetHeroHealth(HeroCurrentHealth + amount);
    }

    public void HealHeroToFull()
    {
        if (!HasHeroState) return;
        SetHeroHealth(HeroMaxHealth);
    }

    public void DamageHero(int amount)
    {
        if (!HasHeroState || amount <= 0) return;
        SetHeroHealth(HeroCurrentHealth - amount);
    }

    public void ChangeHeroMaxHealth(int amount, bool healByIncrease = true)
    {
        if (!HasHeroState || amount == 0) return;

        int oldMaxHealth = HeroMaxHealth;
        HeroMaxHealth = Mathf.Max(1, HeroMaxHealth + amount);

        if (healByIncrease && amount > 0)
        {
            HeroCurrentHealth += HeroMaxHealth - oldMaxHealth;
        }

        HeroCurrentHealth = Mathf.Clamp(HeroCurrentHealth, 0, HeroMaxHealth);
        NotifyRunStateChanged();
    }

    public void SetStress(int amount)
    {
        if (!HasHeroState) return;

        foreach (RunHeroStressState stressState in heroStressStates)
        {
            stressState.SetStress(amount);
        }

        RefreshAggregateStress();
        NotifyRunStateChanged();
    }

    public void SetHeroStress(int heroIndex, int amount)
    {
        if (!HasHeroState || heroIndex < 0 || heroIndex >= heroStressStates.Count) return;

        heroStressStates[heroIndex].SetStress(amount);
        RefreshAggregateStress();
        NotifyRunStateChanged();
    }

    public void AddStress(int amount)
    {
        if (!HasHeroState || amount <= 0) return;

        foreach (RunHeroStressState stressState in heroStressStates)
        {
            stressState.AddStress(amount);
        }

        RefreshAggregateStress();
        NotifyRunStateChanged();
    }

    public void AddStressToHero(int heroIndex, int amount)
    {
        if (!HasHeroState || heroIndex < 0 || heroIndex >= heroStressStates.Count || amount <= 0) return;

        heroStressStates[heroIndex].AddStress(amount);
        RefreshAggregateStress();
        NotifyRunStateChanged();
    }

    public void ReduceStress(int amount)
    {
        if (!HasHeroState || amount <= 0) return;

        foreach (RunHeroStressState stressState in heroStressStates)
        {
            stressState.ReduceStress(amount);
        }

        RefreshAggregateStress();
        NotifyRunStateChanged();
    }

    public void ClearStress()
    {
        if (!HasHeroState) return;

        foreach (RunHeroStressState stressState in heroStressStates)
        {
            stressState.ClearStress();
        }

        RefreshAggregateStress();
        NotifyRunStateChanged();
    }

    public void ChangeMaxStress(int amount)
    {
        if (!HasHeroState || amount == 0) return;

        foreach (RunHeroStressState stressState in heroStressStates)
        {
            stressState.ChangeMaxStress(amount);
        }

        RefreshAggregateStress();
        NotifyRunStateChanged();
    }

    public void SetGold(int amount)
    {
        Gold = Mathf.Max(0, amount);
        NotifyRunStateChanged();
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        SetGold(Gold + amount);
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (Gold < amount) return false;

        SetGold(Gold - amount);
        return true;
    }

    public void AbandonRun()
    {
        CurrentMap = null;
        CurrentMapNodeId = null;
        SelectedEncounter = null;
        ClearHeroState();
        Gold = 0;
        Counters.Clear();
        CardCostModifiers.Clear();

        if (RunDeckManager.Instance != null)
        {
            RunDeckManager.Instance.ClearDeck();
        }

        if (RunPotionManager.Instance != null)
        {
            RunPotionManager.Instance.ClearPotions();
        }

        NotifyRunStateChanged();
    }

    private void ClearHeroState()
    {
        HasHeroState = false;
        HeroCurrentHealth = 0;
        HeroMaxHealth = 0;
        HeroCurrentStress = 0;
        HeroMaxStress = 0;
        heroStressStates.Clear();
    }

    private static int GetTotalHeroHealth(IReadOnlyList<HeroData> heroTeam)
    {
        int totalHealth = 0;
        if (heroTeam == null) return totalHealth;

        foreach (HeroData heroData in heroTeam)
        {
            if (heroData != null) totalHealth += heroData.Health;
        }

        return totalHealth;
    }

    private static int CountValidHeroes(IReadOnlyList<HeroData> heroTeam)
    {
        int count = 0;
        if (heroTeam == null) return count;

        foreach (HeroData heroData in heroTeam)
        {
            if (heroData != null) count++;
        }

        return count;
    }

    private void RebuildHeroStressStates(IReadOnlyList<HeroData> heroTeam, int maxStress, int currentStress)
    {
        heroStressStates.Clear();

        if (heroTeam != null)
        {
            foreach (HeroData heroData in heroTeam)
            {
                if (heroData == null) continue;
                heroStressStates.Add(new RunHeroStressState(heroData, maxStress, currentStress));
            }
        }

        if (heroStressStates.Count == 0)
        {
            heroStressStates.Add(new RunHeroStressState(null, maxStress, currentStress));
        }
    }

    private void RefreshAggregateStress()
    {
        HeroCurrentStress = 0;
        HeroMaxStress = 0;

        foreach (RunHeroStressState stressState in heroStressStates)
        {
            if (stressState == null) continue;
            HeroCurrentStress = Mathf.Max(HeroCurrentStress, stressState.CurrentStress);
            HeroMaxStress = Mathf.Max(HeroMaxStress, stressState.MaxStress);
        }
    }

    private void NotifyRunStateChanged()
    {
        RunStateChanged?.Invoke();
    }
}
